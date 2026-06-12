#pragma once

#include "XpsPageWatermarker.g.h"

namespace winrt::PrintSink::Xps::implementation
{
    struct XpsPageWatermarker : XpsPageWatermarkerT<XpsPageWatermarker>
    {
        XpsPageWatermarker();

        winrt::hstring Text() const;
        void Text(winrt::hstring const& value);
        winrt::hstring FontFamily() const;
        void FontFamily(winrt::hstring const& value);
        double FontSize() const;
        void FontSize(double value);
        double Opacity() const;
        void Opacity(double value);
        double RotationDegrees() const;
        void RotationDegrees(double value);
        double OffsetX() const;
        void OffsetX(double value);
        double OffsetY() const;
        void OffsetY(double value);
        void ApplyWatermarksToXpsPage(winrt::com_ptr<IXpsOMPage> const& xpsPage);

    private:
        winrt::com_ptr<IXpsOMObjectFactory1> xpsFactory;
        winrt::hstring text;
        winrt::hstring fontFamily{ L"Segoe UI" };
        double fontSize{ 48.0 };
        double opacity{ 0.35 };
        double rotationDegrees{ -30.0 };
        double offsetX{ 0.0 };
        double offsetY{ 0.0 };

        void AddWatermarkText(winrt::com_ptr<IXpsOMPage> const& xpsPage);
        winrt::com_ptr<IXpsOMFontResource> CreateFontResource(std::wstring const& fontFilePath);
        winrt::com_ptr<IXpsOMSolidColorBrush> CreateTextBrush();
        static std::wstring ResolveFontPath(winrt::hstring const& requestedFontFamily);
        static uint8_t ToAlpha(double opacityValue);
    };
}

namespace winrt::PrintSink::Xps::factory_implementation
{
    struct XpsPageWatermarker : XpsPageWatermarkerT<XpsPageWatermarker, implementation::XpsPageWatermarker>
    {
    };
}
