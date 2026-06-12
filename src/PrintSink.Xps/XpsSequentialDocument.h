#pragma once

#include "SynchronizedSequentialStream.h"
#include "XpsPageWatermarker.h"
#include "XpsSequentialDocument.g.h"

namespace winrt::PrintSink::Xps::implementation
{
    struct XpsSequentialDocument :
        XpsSequentialDocumentT<XpsSequentialDocument, IPrintWorkflowXpsReceiver2>
    {
        XpsSequentialDocument() = default;
        XpsSequentialDocument(
            winrt::Windows::Graphics::Printing::Workflow::PrintWorkflowObjectModelSourceFileContent const& sourceFileContent);

        void StartXpsOMGeneration();
        winrt::Windows::Storage::Streams::IInputStream GetWatermarkedStream(
            winrt::PrintSink::Xps::XpsPageWatermarker const& pageWatermarker);
        uint32_t PageCount() const;

        winrt::event_token PageAdded(
            winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint32_t> const& handler);
        void PageAdded(winrt::event_token const& token) noexcept;

        winrt::event_token DocumentClosed(
            winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint32_t> const& handler);
        void DocumentClosed(winrt::event_token const& token) noexcept;

        winrt::event_token XpsGenerationFailed(
            winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint64_t> const& handler);
        void XpsGenerationFailed(winrt::event_token const& token) noexcept;

        HRESULT STDMETHODCALLTYPE SetDocumentSequencePrintTicket(IStream* documentSequencePrintTicket) noexcept;
        HRESULT STDMETHODCALLTYPE SetDocumentSequenceUri(PCWSTR documentSequenceUri) noexcept;
        HRESULT STDMETHODCALLTYPE AddDocumentData(UINT32 documentId, IStream* documentPrintTicket, PCWSTR documentUri) noexcept;
        HRESULT STDMETHODCALLTYPE AddPage(UINT32 documentId, UINT32 pageId, IXpsOMPageReference* pageReference, PCWSTR pageUri) noexcept;
        HRESULT STDMETHODCALLTYPE Close() noexcept;
        HRESULT STDMETHODCALLTYPE Failed(HRESULT xpsError) noexcept;

    private:
        winrt::com_ptr<IPrintWorkflowObjectModelSourceFileContentNative> sourceFileContent;
        winrt::com_ptr<XpsPageWatermarker> watermarker;
        winrt::com_ptr<SynchronizedSequentialStream> outputStream;
        winrt::com_ptr<IXpsOMObjectFactory1> xpsFactory;
        winrt::com_ptr<IXpsOMPackageWriter> packageWriter;
        uint32_t pageCount{ 0 };

        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint32_t>> pageAdded;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint32_t>> documentClosed;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::PrintSink::Xps::XpsSequentialDocument, uint64_t>> xpsGenerationFailed;
    };
}

namespace winrt::PrintSink::Xps::factory_implementation
{
    struct XpsSequentialDocument :
        XpsSequentialDocumentT<XpsSequentialDocument, implementation::XpsSequentialDocument>
    {
    };
}
