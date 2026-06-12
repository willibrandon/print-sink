#include "pch.h"
#include "XpsPageWatermarker.h"
#include "XpsPageWatermarker.g.cpp"
#include <shcore.h>

namespace winrt::PrintSink::Xps::implementation
{
    XpsPageWatermarker::XpsPageWatermarker()
    {
        xpsFactory = winrt::create_instance<IXpsOMObjectFactory1>(winrt::guid_of<XpsOMObjectFactory>());
    }

    winrt::hstring XpsPageWatermarker::Text() const
    {
        return text;
    }

    void XpsPageWatermarker::Text(winrt::hstring const& value)
    {
        text = value;
    }

    winrt::hstring XpsPageWatermarker::FontFamily() const
    {
        return fontFamily;
    }

    void XpsPageWatermarker::FontFamily(winrt::hstring const& value)
    {
        fontFamily = value;
    }

    double XpsPageWatermarker::FontSize() const
    {
        return fontSize;
    }

    void XpsPageWatermarker::FontSize(double value)
    {
        fontSize = value;
    }

    double XpsPageWatermarker::Opacity() const
    {
        return opacity;
    }

    void XpsPageWatermarker::Opacity(double value)
    {
        opacity = value;
    }

    double XpsPageWatermarker::RotationDegrees() const
    {
        return rotationDegrees;
    }

    void XpsPageWatermarker::RotationDegrees(double value)
    {
        rotationDegrees = value;
    }

    double XpsPageWatermarker::OffsetX() const
    {
        return offsetX;
    }

    void XpsPageWatermarker::OffsetX(double value)
    {
        offsetX = value;
    }

    double XpsPageWatermarker::OffsetY() const
    {
        return offsetY;
    }

    void XpsPageWatermarker::OffsetY(double value)
    {
        offsetY = value;
    }

    winrt::hstring XpsPageWatermarker::ImagePath() const
    {
        return imagePath;
    }

    void XpsPageWatermarker::ImagePath(winrt::hstring const& value)
    {
        imagePath = value;
    }

    double XpsPageWatermarker::ImageWidth() const
    {
        return imageWidth;
    }

    void XpsPageWatermarker::ImageWidth(double value)
    {
        imageWidth = value;
    }

    double XpsPageWatermarker::ImageHeight() const
    {
        return imageHeight;
    }

    void XpsPageWatermarker::ImageHeight(double value)
    {
        imageHeight = value;
    }

    double XpsPageWatermarker::ImageOpacity() const
    {
        return imageOpacity;
    }

    void XpsPageWatermarker::ImageOpacity(double value)
    {
        imageOpacity = value;
    }

    double XpsPageWatermarker::ImageRotationDegrees() const
    {
        return imageRotationDegrees;
    }

    void XpsPageWatermarker::ImageRotationDegrees(double value)
    {
        imageRotationDegrees = value;
    }

    double XpsPageWatermarker::ImageOffsetX() const
    {
        return imageOffsetX;
    }

    void XpsPageWatermarker::ImageOffsetX(double value)
    {
        imageOffsetX = value;
    }

    double XpsPageWatermarker::ImageOffsetY() const
    {
        return imageOffsetY;
    }

    void XpsPageWatermarker::ImageOffsetY(double value)
    {
        imageOffsetY = value;
    }

    winrt::Windows::Storage::Streams::IRandomAccessStream XpsPageWatermarker::ApplyToPackage(
        winrt::Windows::Storage::Streams::IRandomAccessStream const& source)
    {
        winrt::com_ptr<IStream> inputStream;
        winrt::check_hresult(::CreateStreamOverRandomAccessStream(
            winrt::get_unknown(source),
            __uuidof(IStream),
            inputStream.put_void()));

        winrt::com_ptr<IXpsOMPackage> package;
        winrt::check_hresult(xpsFactory->CreatePackageFromStream(inputStream.get(), FALSE, package.put()));
        ApplyWatermarksToPackage(package);

        winrt::Windows::Storage::Streams::InMemoryRandomAccessStream output;
        winrt::com_ptr<IStream> outputStream;
        winrt::check_hresult(::CreateStreamOverRandomAccessStream(
            winrt::get_unknown(output),
            __uuidof(IStream),
            outputStream.put_void()));

        winrt::com_ptr<IXpsOMPackage1> package1 = package.try_as<IXpsOMPackage1>();
        if (package1)
        {
            XPS_DOCUMENT_TYPE documentType{};
            winrt::check_hresult(package1->GetDocumentType(&documentType));
            winrt::check_hresult(package1->WriteToStream1(outputStream.get(), FALSE, documentType));
        }
        else
        {
            winrt::check_hresult(package->WriteToStream(outputStream.get(), FALSE));
        }

        output.Seek(0);
        return output;
    }

    void XpsPageWatermarker::ApplyWatermarksToXpsPage(winrt::com_ptr<IXpsOMPage> const& xpsPage)
    {
        if (!text.empty())
        {
            AddWatermarkText(xpsPage);
        }

        if (!imagePath.empty())
        {
            AddWatermarkImage(xpsPage);
        }
    }

    void XpsPageWatermarker::AddWatermarkText(winrt::com_ptr<IXpsOMPage> const& xpsPage)
    {
        XPS_SIZE pageDimensions{};
        winrt::check_hresult(xpsPage->GetPageDimensions(&pageDimensions));

        winrt::com_ptr<IXpsOMSolidColorBrush> textBrush = CreateTextBrush();
        winrt::com_ptr<IXpsOMFontResource> fontResource = CreateFontResource(ResolveFontPath(fontFamily));

        winrt::com_ptr<IXpsOMGlyphs> glyphs;
        winrt::check_hresult(xpsFactory->CreateGlyphs(fontResource.get(), glyphs.put()));

        XPS_POINT origin{ 0.0f, 0.0f };
        winrt::check_hresult(glyphs->SetOrigin(&origin));
        winrt::check_hresult(glyphs->SetFontRenderingEmSize(static_cast<float>(fontSize)));
        winrt::check_hresult(glyphs->SetFillBrushLocal(textBrush.get()));

        winrt::com_ptr<IXpsOMGlyphsEditor> glyphsEditor;
        winrt::check_hresult(glyphs->GetGlyphsEditor(glyphsEditor.put()));
        winrt::check_hresult(glyphsEditor->SetUnicodeString(text.c_str()));
        winrt::check_hresult(glyphsEditor->ApplyEdits());

        constexpr double pi = 3.14159265358979323846;
        double radians = rotationDegrees * pi / 180.0;
        float cosValue = static_cast<float>(std::cos(radians));
        float sinValue = static_cast<float>(std::sin(radians));
        XPS_MATRIX matrix
        {
            cosValue,
            sinValue,
            -sinValue,
            cosValue,
            static_cast<float>((pageDimensions.width / 4.0) + offsetX),
            static_cast<float>((pageDimensions.height / 2.0) + offsetY),
        };

        winrt::com_ptr<IXpsOMMatrixTransform> transform;
        winrt::check_hresult(xpsFactory->CreateMatrixTransform(&matrix, transform.put()));
        winrt::check_hresult(glyphs->SetTransformLocal(transform.get()));

        winrt::com_ptr<IXpsOMVisualCollection> pageVisuals;
        winrt::check_hresult(xpsPage->GetVisuals(pageVisuals.put()));
        winrt::check_hresult(pageVisuals->Append(glyphs.get()));
    }

    void XpsPageWatermarker::AddWatermarkImage(winrt::com_ptr<IXpsOMPage> const& xpsPage)
    {
        XPS_SIZE pageDimensions{};
        winrt::check_hresult(xpsPage->GetPageDimensions(&pageDimensions));

        double width = std::clamp(imageWidth, 1.0, static_cast<double>(pageDimensions.width));
        double height = std::clamp(imageHeight, 1.0, static_cast<double>(pageDimensions.height));
        double x = ((pageDimensions.width - width) / 2.0) + imageOffsetX;
        double y = ((pageDimensions.height - height) / 2.0) + imageOffsetY;

        XPS_RECT imageRect
        {
            static_cast<float>(x),
            static_cast<float>(y),
            static_cast<float>(width),
            static_cast<float>(height),
        };
        XPS_RECT viewBox
        {
            0.0f,
            0.0f,
            static_cast<float>(width),
            static_cast<float>(height),
        };
        XPS_RECT viewPort
        {
            imageRect.x,
            imageRect.y,
            imageRect.width,
            imageRect.height,
        };

        winrt::com_ptr<IXpsOMImageResource> imageResource = CreateImageResource(std::wstring{ imagePath.c_str() });
        winrt::com_ptr<IXpsOMImageBrush> imageBrush;
        winrt::check_hresult(xpsFactory->CreateImageBrush(imageResource.get(), &viewBox, &viewPort, imageBrush.put()));
        float clampedOpacity = static_cast<float>(std::clamp(imageOpacity, 0.0, 1.0));
        winrt::check_hresult(imageBrush->SetOpacity(clampedOpacity));

        winrt::com_ptr<IXpsOMPath> imagePathVisual = CreateRectanglePath(imageRect);
        winrt::check_hresult(imagePathVisual->SetAccessibilityShortDescription(L"PrintSink image watermark"));
        winrt::check_hresult(imagePathVisual->SetFillBrushLocal(imageBrush.get()));

        double centerX = imageRect.x + (imageRect.width / 2.0);
        double centerY = imageRect.y + (imageRect.height / 2.0);
        XPS_MATRIX matrix = CreateRotationMatrix(imageRotationDegrees, centerX, centerY);
        winrt::com_ptr<IXpsOMMatrixTransform> transform;
        winrt::check_hresult(xpsFactory->CreateMatrixTransform(&matrix, transform.put()));
        winrt::check_hresult(imagePathVisual->SetTransformLocal(transform.get()));

        winrt::com_ptr<IXpsOMVisualCollection> pageVisuals;
        winrt::check_hresult(xpsPage->GetVisuals(pageVisuals.put()));
        winrt::check_hresult(pageVisuals->Append(imagePathVisual.get()));
    }

    void XpsPageWatermarker::ApplyWatermarksToPackage(winrt::com_ptr<IXpsOMPackage> const& package)
    {
        winrt::com_ptr<IXpsOMDocumentSequence> documentSequence;
        winrt::check_hresult(package->GetDocumentSequence(documentSequence.put()));

        winrt::com_ptr<IXpsOMDocumentCollection> documents;
        winrt::check_hresult(documentSequence->GetDocuments(documents.put()));

        UINT32 documentCount{};
        winrt::check_hresult(documents->GetCount(&documentCount));
        for (UINT32 documentIndex = 0; documentIndex < documentCount; ++documentIndex)
        {
            winrt::com_ptr<IXpsOMDocument> document;
            winrt::check_hresult(documents->GetAt(documentIndex, document.put()));

            winrt::com_ptr<IXpsOMPageReferenceCollection> pageReferences;
            winrt::check_hresult(document->GetPageReferences(pageReferences.put()));

            UINT32 pageCount{};
            winrt::check_hresult(pageReferences->GetCount(&pageCount));
            for (UINT32 pageIndex = 0; pageIndex < pageCount; ++pageIndex)
            {
                winrt::com_ptr<IXpsOMPageReference> pageReference;
                winrt::check_hresult(pageReferences->GetAt(pageIndex, pageReference.put()));

                winrt::com_ptr<IXpsOMPage> page;
                winrt::check_hresult(pageReference->GetPage(page.put()));
                ApplyWatermarksToXpsPage(page);
            }
        }
    }

    winrt::com_ptr<IXpsOMFontResource> XpsPageWatermarker::CreateFontResource(std::wstring const& fontFilePath)
    {
        winrt::com_ptr<IOpcPartUri> fontUri;
        winrt::check_hresult(xpsFactory->CreatePartUri(L"/Resources/Fonts/PrintSinkWatermark.odttf", fontUri.put()));

        winrt::com_ptr<IStream> fontStream;
        winrt::check_hresult(xpsFactory->CreateReadOnlyStreamOnFile(fontFilePath.c_str(), fontStream.put()));

        winrt::com_ptr<IXpsOMFontResource> fontResource;
        winrt::check_hresult(xpsFactory->CreateFontResource(
            fontStream.get(),
            XPS_FONT_EMBEDDING_NORMAL,
            fontUri.get(),
            FALSE,
            fontResource.put()));

        return fontResource;
    }

    winrt::com_ptr<IXpsOMSolidColorBrush> XpsPageWatermarker::CreateTextBrush()
    {
        XPS_COLOR color{};
        color.colorType = XPS_COLOR_TYPE_SRGB;
        color.value.sRGB.alpha = ToAlpha(opacity);
        color.value.sRGB.red = 0;
        color.value.sRGB.green = 0;
        color.value.sRGB.blue = 0;

        winrt::com_ptr<IXpsOMSolidColorBrush> brush;
        winrt::check_hresult(xpsFactory->CreateSolidColorBrush(&color, nullptr, brush.put()));
        return brush;
    }

    winrt::com_ptr<IXpsOMImageResource> XpsPageWatermarker::CreateImageResource(std::wstring const& imageFilePath)
    {
        winrt::com_ptr<IStream> imageStream;
        winrt::check_hresult(xpsFactory->CreateReadOnlyStreamOnFile(imageFilePath.c_str(), imageStream.put()));

        size_t extensionIndex = imageFilePath.find_last_of(L'.');
        if (extensionIndex == std::wstring::npos)
        {
            winrt::throw_hresult(E_INVALIDARG);
        }

        std::wstring extension = imageFilePath.substr(extensionIndex);
        std::transform(extension.begin(), extension.end(), extension.begin(), [](wchar_t value)
        {
            return static_cast<wchar_t>(std::towlower(value));
        });

        std::wstring imagePartName = L"/Resources/Images/PrintSinkWatermarkImage";
        imagePartName += extension;

        winrt::com_ptr<IOpcPartUri> imageUri;
        winrt::check_hresult(xpsFactory->CreatePartUri(imagePartName.c_str(), imageUri.put()));

        winrt::com_ptr<IXpsOMImageResource> imageResource;
        winrt::check_hresult(xpsFactory->CreateImageResource(
            imageStream.get(),
            ResolveImageType(imageFilePath),
            imageUri.get(),
            imageResource.put()));
        return imageResource;
    }

    winrt::com_ptr<IXpsOMPath> XpsPageWatermarker::CreateRectanglePath(XPS_RECT const& rect)
    {
        XPS_POINT startPoint{ rect.x, rect.y };
        XPS_SEGMENT_TYPE segmentTypes[3]
        {
            XPS_SEGMENT_TYPE_LINE,
            XPS_SEGMENT_TYPE_LINE,
            XPS_SEGMENT_TYPE_LINE,
        };
        FLOAT segmentData[6]
        {
            rect.x,
            rect.y + rect.height,
            rect.x + rect.width,
            rect.y + rect.height,
            rect.x + rect.width,
            rect.y,
        };
        BOOL segmentStrokes[3]{ TRUE, TRUE, TRUE };

        winrt::com_ptr<IXpsOMGeometryFigure> figure;
        winrt::check_hresult(xpsFactory->CreateGeometryFigure(&startPoint, figure.put()));
        winrt::check_hresult(figure->SetIsClosed(TRUE));
        winrt::check_hresult(figure->SetIsFilled(TRUE));
        winrt::check_hresult(figure->SetSegments(3, 6, segmentTypes, segmentData, segmentStrokes));

        winrt::com_ptr<IXpsOMGeometry> geometry;
        winrt::check_hresult(xpsFactory->CreateGeometry(geometry.put()));

        winrt::com_ptr<IXpsOMGeometryFigureCollection> figures;
        winrt::check_hresult(geometry->GetFigures(figures.put()));
        winrt::check_hresult(figures->Append(figure.get()));

        winrt::com_ptr<IXpsOMPath> path;
        winrt::check_hresult(xpsFactory->CreatePath(path.put()));
        winrt::check_hresult(path->SetGeometryLocal(geometry.get()));
        return path;
    }

    XPS_IMAGE_TYPE XpsPageWatermarker::ResolveImageType(std::wstring const& imageFilePath) const
    {
        size_t extensionIndex = imageFilePath.find_last_of(L'.');
        if (extensionIndex == std::wstring::npos)
        {
            winrt::throw_hresult(E_INVALIDARG);
        }

        std::wstring extension = imageFilePath.substr(extensionIndex + 1);
        std::transform(extension.begin(), extension.end(), extension.begin(), [](wchar_t value)
        {
            return static_cast<wchar_t>(std::towlower(value));
        });

        if (extension == L"jpg" || extension == L"jpeg")
        {
            return XPS_IMAGE_TYPE_JPEG;
        }

        if (extension == L"png")
        {
            return XPS_IMAGE_TYPE_PNG;
        }

        if (extension == L"tif" || extension == L"tiff")
        {
            return XPS_IMAGE_TYPE_TIFF;
        }

        if (extension == L"wdp")
        {
            return XPS_IMAGE_TYPE_WDP;
        }

        if (extension == L"jxr")
        {
            return XPS_IMAGE_TYPE_JXR;
        }

        winrt::throw_hresult(E_INVALIDARG);
    }

    std::wstring XpsPageWatermarker::ResolveFontPath(winrt::hstring const& requestedFontFamily)
    {
        wchar_t windowsDirectory[MAX_PATH]{};
        UINT length = ::GetSystemWindowsDirectoryW(windowsDirectory, ARRAYSIZE(windowsDirectory));
        if (length == 0 || length >= ARRAYSIZE(windowsDirectory))
        {
            winrt::throw_hresult(HRESULT_FROM_WIN32(::GetLastError()));
        }

        std::wstring family{ requestedFontFamily.c_str() };
        std::transform(family.begin(), family.end(), family.begin(), [](wchar_t value)
        {
            return static_cast<wchar_t>(std::towlower(value));
        });

        std::wstring fileName = family.find(L"arial") != std::wstring::npos
            ? L"arial.ttf"
            : L"segoeui.ttf";

        std::wstring path{ windowsDirectory };
        path += L"\\Fonts\\";
        path += fileName;
        return path;
    }

    XPS_MATRIX XpsPageWatermarker::CreateRotationMatrix(double rotation, double centerX, double centerY)
    {
        constexpr double pi = 3.14159265358979323846;
        double radians = rotation * pi / 180.0;
        double cosValue = std::cos(radians);
        double sinValue = std::sin(radians);

        return XPS_MATRIX
        {
            static_cast<float>(cosValue),
            static_cast<float>(sinValue),
            static_cast<float>(-sinValue),
            static_cast<float>(cosValue),
            static_cast<float>(centerX - (cosValue * centerX) + (sinValue * centerY)),
            static_cast<float>(centerY - (sinValue * centerX) - (cosValue * centerY)),
        };
    }

    uint8_t XpsPageWatermarker::ToAlpha(double opacityValue)
    {
        double clamped = std::clamp(opacityValue, 0.0, 1.0);
        return static_cast<uint8_t>(std::round(clamped * 255.0));
    }
}
