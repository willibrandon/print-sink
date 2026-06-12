#include "pch.h"
#include "SynchronizedSequentialStream.h"

using namespace winrt::Windows::Security::Cryptography;
using namespace winrt::Windows::Storage::Streams;

namespace winrt::PrintSink::Xps::implementation
{
    SynchronizedSequentialStream::SynchronizedSequentialStream()
    {
        streamWritten.attach(::CreateEventW(nullptr, false, false, nullptr));
        winrt::check_bool(static_cast<bool>(streamWritten));

        outputStream = storage.GetOutputStreamAt(0);
    }

    STDMETHODIMP SynchronizedSequentialStream::Read(void* buffer, ULONG count, ULONG* bytesRead) noexcept try
    {
        if (bytesRead != nullptr)
        {
            *bytesRead = 0;
        }

        if (buffer == nullptr)
        {
            return STG_E_INVALIDPOINTER;
        }

        uint64_t bytesAvailable = WaitForBytes(count);
        ULONG bytesToRead = count < bytesAvailable
            ? count
            : static_cast<ULONG>(bytesAvailable);

        if (bytesToRead == 0)
        {
            return count == 0 ? S_OK : S_FALSE;
        }

        IBuffer resultBuffer{ nullptr };
        {
            auto lock = streamAccess.LockExclusive();
            IInputStream inputStream = storage.GetInputStreamAt(readIndex);
            Buffer readBuffer{ bytesToRead };
            resultBuffer = inputStream.ReadAsync(readBuffer, bytesToRead, InputStreamOptions::Partial).get();
        }

        winrt::com_array<uint8_t> bytes;
        CryptographicBuffer::CopyToByteArray(resultBuffer, bytes);
        if (!bytes.empty())
        {
            std::memcpy(buffer, bytes.data(), bytes.size());
        }

        readIndex += bytes.size();
        if (bytesRead != nullptr)
        {
            *bytesRead = static_cast<ULONG>(bytes.size());
        }

        return bytes.size() == count ? S_OK : S_FALSE;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    STDMETHODIMP SynchronizedSequentialStream::Write(void const* buffer, ULONG count, ULONG* bytesWritten) noexcept try
    {
        BYTE const* first = static_cast<BYTE const*>(buffer);
        BYTE const* last = first + count;
        IBuffer winrtBuffer = CryptographicBuffer::CreateFromByteArray(std::vector<BYTE>(first, last));

        {
            auto lock = streamAccess.LockExclusive();
            outputStream.WriteAsync(winrtBuffer).get();
        }

        ::SetEvent(streamWritten.get());

        if (bytesWritten != nullptr)
        {
            *bytesWritten = count;
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    winrt::Windows::Foundation::IAsyncOperationWithProgress<IBuffer, uint32_t>
        SynchronizedSequentialStream::ReadAsync(
            IBuffer buffer,
            uint32_t count,
            InputStreamOptions options)
    {
        co_await winrt::resume_background();

        uint64_t bytesAvailable = WaitForBytes(count);
        uint32_t bytesToRead = count < bytesAvailable
            ? count
            : static_cast<uint32_t>(bytesAvailable);

        if (bytesToRead == 0)
        {
            buffer.Length(0);
            co_return buffer;
        }

        IBuffer resultBuffer{ nullptr };
        {
            auto lock = streamAccess.LockExclusive();
            IInputStream inputStream = storage.GetInputStreamAt(readIndex);
            resultBuffer = inputStream.ReadAsync(buffer, bytesToRead, options).get();
        }

        readIndex += bytesToRead;
        co_return resultBuffer;
    }

    void SynchronizedSequentialStream::Close()
    {
        streamClosed = true;
        ::SetEvent(streamWritten.get());
    }

    uint64_t SynchronizedSequentialStream::WaitForBytes(uint64_t byteCount)
    {
        uint64_t bytesAvailable = AvailableBytes();
        while (byteCount > bytesAvailable && !streamClosed)
        {
            DWORD waitResult = ::WaitForSingleObject(streamWritten.get(), INFINITE);
            if (waitResult != WAIT_OBJECT_0)
            {
                winrt::throw_hresult(E_UNEXPECTED);
            }

            bytesAvailable = AvailableBytes();
        }

        return bytesAvailable;
    }

    uint64_t SynchronizedSequentialStream::AvailableBytes()
    {
        auto lock = streamAccess.LockExclusive();
        return storage.Size() - readIndex;
    }
}
