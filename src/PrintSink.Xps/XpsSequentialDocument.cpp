#include "pch.h"
#include "XpsSequentialDocument.h"
#include "XpsSequentialDocument.g.cpp"

namespace winrt::PrintSink::Xps::implementation
{
    XpsSequentialDocument::XpsSequentialDocument(
        winrt::Windows::Graphics::Printing::Workflow::PrintWorkflowObjectModelSourceFileContent const& sourceFileContent)
    {
        this->sourceFileContent = sourceFileContent.as<IPrintWorkflowObjectModelSourceFileContentNative>();
        xpsFactory = winrt::create_instance<IXpsOMObjectFactory1>(winrt::guid_of<XpsOMObjectFactory>());
    }

    void XpsSequentialDocument::StartXpsOMGeneration()
    {
        winrt::check_hresult(sourceFileContent->StartXpsOMGeneration(this));
    }

    winrt::Windows::Storage::Streams::IInputStream XpsSequentialDocument::GetWatermarkedStream(
        winrt::PrintSink::Xps::XpsPageWatermarker const& pageWatermarker)
    {
        outputStream = winrt::make_self<SynchronizedSequentialStream>();
        watermarker = pageWatermarker.as<XpsPageWatermarker>();
        StartXpsOMGeneration();
        return outputStream.as<winrt::Windows::Storage::Streams::IInputStream>();
    }

    uint32_t XpsSequentialDocument::PageCount() const
    {
        return pageCount;
    }

    winrt::event_token XpsSequentialDocument::PageAdded(
        winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint32_t> const& handler)
    {
        return pageAdded.add(handler);
    }

    void XpsSequentialDocument::PageAdded(winrt::event_token const& token) noexcept
    {
        pageAdded.remove(token);
    }

    winrt::event_token XpsSequentialDocument::DocumentClosed(
        winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint32_t> const& handler)
    {
        return documentClosed.add(handler);
    }

    void XpsSequentialDocument::DocumentClosed(winrt::event_token const& token) noexcept
    {
        documentClosed.remove(token);
    }

    winrt::event_token XpsSequentialDocument::XpsGenerationFailed(
        winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint64_t> const& handler)
    {
        return xpsGenerationFailed.add(handler);
    }

    void XpsSequentialDocument::XpsGenerationFailed(winrt::event_token const& token) noexcept
    {
        xpsGenerationFailed.remove(token);
    }

    HRESULT STDMETHODCALLTYPE XpsSequentialDocument::SetDocumentSequencePrintTicket(
        [[maybe_unused]] IStream* documentSequencePrintTicket) noexcept
    {
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE XpsSequentialDocument::SetDocumentSequenceUri(PCWSTR documentSequenceUri) noexcept try
    {
        if (outputStream != nullptr)
        {
            winrt::com_ptr<IOpcPartUri> documentSequencePartName;
            winrt::check_hresult(xpsFactory->CreatePartUri(documentSequenceUri, documentSequencePartName.put()));

            winrt::com_ptr<IOpcPartUri> discardControlPartName;
            winrt::check_hresult(xpsFactory->CreatePartUri(L"/DiscardControl.xml", discardControlPartName.put()));

            winrt::check_hresult(xpsFactory->CreatePackageWriterOnStream1(
                outputStream.as<ISequentialStream>().get(),
                TRUE,
                XPS_INTERLEAVING_ON,
                documentSequencePartName.get(),
                nullptr,
                nullptr,
                nullptr,
                discardControlPartName.get(),
                XPS_DOCUMENT_TYPE_OPENXPS,
                packageWriter.put()));
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    HRESULT STDMETHODCALLTYPE XpsSequentialDocument::AddDocumentData(
        [[maybe_unused]] UINT32 documentId,
        [[maybe_unused]] IStream* documentPrintTicket,
        PCWSTR documentUri) noexcept try
    {
        if (packageWriter != nullptr)
        {
            winrt::com_ptr<IOpcPartUri> documentPartUri;
            winrt::check_hresult(xpsFactory->CreatePartUri(documentUri, documentPartUri.put()));
            winrt::check_hresult(packageWriter->StartNewDocument(documentPartUri.get(), nullptr, nullptr, nullptr, nullptr));
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    HRESULT STDMETHODCALLTYPE XpsSequentialDocument::AddPage(
        [[maybe_unused]] UINT32 documentId,
        UINT32 pageId,
        IXpsOMPageReference* pageReference,
        [[maybe_unused]] PCWSTR pageUri) noexcept try
    {
        winrt::com_ptr<IXpsOMPage> page;
        winrt::check_hresult(pageReference->GetPage(page.put()));

        if (packageWriter != nullptr)
        {
            XPS_SIZE pageDimensions{};
            winrt::check_hresult(page->GetPageDimensions(&pageDimensions));

            if (watermarker != nullptr)
            {
                watermarker->ApplyWatermarksToXpsPage(page);
            }

            winrt::check_hresult(packageWriter->AddPage(page.get(), &pageDimensions, nullptr, nullptr, nullptr, nullptr));
        }

        pageCount += 1;

        try
        {
            pageAdded(*this, pageId);
        }
        catch (...)
        {
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    HRESULT STDMETHODCALLTYPE XpsSequentialDocument::Close() noexcept try
    {
        if (packageWriter != nullptr)
        {
            winrt::check_hresult(packageWriter->Close());
        }

        if (outputStream != nullptr)
        {
            outputStream->Close();
        }

        try
        {
            documentClosed(*this, pageCount);
        }
        catch (...)
        {
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    HRESULT STDMETHODCALLTYPE XpsSequentialDocument::Failed(HRESULT xpsError) noexcept try
    {
        if (outputStream != nullptr)
        {
            outputStream->Close();
        }

        try
        {
            xpsGenerationFailed(*this, static_cast<uint64_t>(xpsError));
        }
        catch (...)
        {
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }
}
