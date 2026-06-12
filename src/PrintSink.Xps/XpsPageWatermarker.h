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
        winrt::hstring ImagePath() const;
        void ImagePath(winrt::hstring const& value);
        double ImageWidth() const;
        void ImageWidth(double value);
        double ImageHeight() const;
        void ImageHeight(double value);
        double ImageOpacity() const;
        void ImageOpacity(double value);
        double ImageRotationDegrees() const;
        void ImageRotationDegrees(double value);
        double ImageOffsetX() const;
        void ImageOffsetX(double value);
        double ImageOffsetY() const;
        void ImageOffsetY(double value);
        winrt::Windows::Storage::Streams::IRandomAccessStream ApplyToPackage(
            winrt::Windows::Storage::Streams::IRandomAccessStream const& source);
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
        winrt::hstring imagePath;
        double imageWidth{ 144.0 };
        double imageHeight{ 144.0 };
        double imageOpacity{ 0.35 };
        double imageRotationDegrees{ 0.0 };
        double imageOffsetX{ 0.0 };
        double imageOffsetY{ 0.0 };

        void AddWatermarkText(winrt::com_ptr<IXpsOMPage> const& xpsPage);
        void AddWatermarkImage(winrt::com_ptr<IXpsOMPage> const& xpsPage);
        void ApplyWatermarksToPackage(winrt::com_ptr<IXpsOMPackage> const& package);
        winrt::com_ptr<IXpsOMFontResource> CreateFontResource(std::wstring const& fontFilePath);
        winrt::com_ptr<IXpsOMSolidColorBrush> CreateTextBrush();
        winrt::com_ptr<IXpsOMImageResource> CreateImageResource(std::wstring const& imageFilePath);
        winrt::com_ptr<IXpsOMPath> CreateRectanglePath(XPS_RECT const& rect);
        XPS_IMAGE_TYPE ResolveImageType(std::wstring const& imageFilePath) const;
        static std::wstring ResolveFontPath(winrt::hstring const& requestedFontFamily);
        static XPS_MATRIX CreateRotationMatrix(double rotation, double centerX, double centerY);
        static uint8_t ToAlpha(double opacityValue);
    };
}

namespace winrt::PrintSink::Xps::factory_implementation
{
    struct XpsPageWatermarker : XpsPageWatermarkerT<XpsPageWatermarker, implementation::XpsPageWatermarker>
    {
    };
}
