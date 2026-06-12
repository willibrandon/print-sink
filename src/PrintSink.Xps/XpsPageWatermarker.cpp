#include "pch.h"
#include "XpsPageWatermarker.h"
#include "XpsPageWatermarker.g.cpp"

namespace winrt::PrintSink::Xps::implementation
{
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
}
