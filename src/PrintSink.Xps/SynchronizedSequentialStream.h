#pragma once

namespace winrt::PrintSink::Xps::implementation
{
    struct SynchronizedSequentialStream :
        winrt::implements<
            SynchronizedSequentialStream,
            ISequentialStream,
            winrt::Windows::Storage::Streams::IInputStream,
            winrt::Windows::Foundation::IClosable>
    {
        SynchronizedSequentialStream();

        STDMETHODIMP Read(void* buffer, ULONG count, ULONG* bytesRead) noexcept override;
        STDMETHODIMP Write(void const* buffer, ULONG count, ULONG* bytesWritten) noexcept override;

        winrt::Windows::Foundation::IAsyncOperationWithProgress<winrt::Windows::Storage::Streams::IBuffer, uint32_t>
            ReadAsync(
                winrt::Windows::Storage::Streams::IBuffer buffer,
                uint32_t count,
                winrt::Windows::Storage::Streams::InputStreamOptions options);

        void Close();

    private:
        uint64_t WaitForBytes(uint64_t byteCount);
        uint64_t AvailableBytes();

        winrt::Windows::Storage::Streams::InMemoryRandomAccessStream storage;
        winrt::Windows::Storage::Streams::IOutputStream outputStream{ nullptr };
        bool streamClosed{ false };
        uint64_t readIndex{ 0 };
        winrt::handle streamWritten;
        Microsoft::WRL::Wrappers::SRWLock streamAccess;
    };
}
