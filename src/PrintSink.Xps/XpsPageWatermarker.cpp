#include "pch.h"
#include "XpsPageWatermarker.h"
#include "XpsPageWatermarker.g.cpp"

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

    void XpsPageWatermarker::ApplyWatermarksToXpsPage(winrt::com_ptr<IXpsOMPage> const& xpsPage)
    {
        if (!text.empty())
        {
            AddWatermarkText(xpsPage);
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

    uint8_t XpsPageWatermarker::ToAlpha(double opacityValue)
    {
        double clamped = std::clamp(opacityValue, 0.0, 1.0);
        return static_cast<uint8_t>(std::round(clamped * 255.0));
    }
}
