#pragma once

#include "XpsPageWatermarker.g.h"

namespace winrt::PrintSink::Xps::implementation
{
    struct XpsPageWatermarker : XpsPageWatermarkerT<XpsPageWatermarker>
    {
        XpsPageWatermarker() = default;

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

    private:
        winrt::hstring text;
        winrt::hstring fontFamily{ L"Segoe UI" };
        double fontSize{ 48.0 };
        double opacity{ 0.35 };
        double rotationDegrees{ -30.0 };
        double offsetX{ 0.0 };
        double offsetY{ 0.0 };
    };
}

namespace winrt::PrintSink::Xps::factory_implementation
{
    struct XpsPageWatermarker : XpsPageWatermarkerT<XpsPageWatermarker, implementation::XpsPageWatermarker>
    {
    };
}
