import Foundation
import ImageIO
import AppKit
import UniformTypeIdentifiers

actor ImageOptimizer {
    static let shared = ImageOptimizer()

    private let heicQuality: CGFloat = 0.78

    private lazy var heicSupported: Bool = {
        let testData = NSMutableData()
        return CGImageDestinationCreateWithData(
            testData,
            UTType.heic.identifier as CFString,
            1, nil
        ) != nil
    }()

    // MARK: - Main Optimization Entry Point

    /// Optimizes a downloaded image: converts to HEIC and/or downscales to screen resolution.
    /// Returns the URL of the optimized file.
    func optimize(sourceURL: URL, destinationURL: URL) throws -> URL {
        let sourceOptions: [CFString: Any] = [kCGImageSourceShouldCache: false]
        guard let imageSource = CGImageSourceCreateWithURL(sourceURL as CFURL, sourceOptions as CFDictionary),
              let properties = CGImageSourceCopyPropertiesAtIndex(imageSource, 0, nil) as? [CFString: Any],
              let srcWidth = properties[kCGImagePropertyPixelWidth] as? CGFloat,
              let srcHeight = properties[kCGImagePropertyPixelHeight] as? CGFloat
        else {
            // Can't read source — just copy it
            try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
            return destinationURL
        }

        let maxDimension = targetMaxPixelDimension()
        let srcMax = max(srcWidth, srcHeight)
        let needsDownscale = srcMax > maxDimension * 1.05

        // Determine output format
        let outputURL: URL
        if heicSupported {
            outputURL = destinationURL.deletingPathExtension().appendingPathExtension("heic")
        } else {
            outputURL = destinationURL.deletingPathExtension().appendingPathExtension("jpg")
        }

        // Get CGImage (with optional downscale)
        // Use kCGImageSourceShouldCache: false to avoid IOSurface pressure on large images
        let cgImage: CGImage
        if needsDownscale {
            let thumbOptions: [CFString: Any] = [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceShouldCacheImmediately: false,
                kCGImageSourceShouldCache: false,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceThumbnailMaxPixelSize: maxDimension
            ]
            guard let thumb = CGImageSourceCreateThumbnailAtIndex(imageSource, 0, thumbOptions as CFDictionary)
            else {
                try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
                return destinationURL
            }
            cgImage = thumb
        } else {
            let readOptions: [CFString: Any] = [
                kCGImageSourceShouldCacheImmediately: false,
                kCGImageSourceShouldCache: false
            ]
            guard let img = CGImageSourceCreateImageAtIndex(imageSource, 0, readOptions as CFDictionary) else {
                try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
                return destinationURL
            }
            cgImage = img
        }

        // Strip alpha for opaque images to avoid HEIC encoder warnings
        // ("trying to save an opaque image with 'AlphaLast'")
        let sourceHasAlpha = properties[kCGImagePropertyHasAlpha] as? Bool ?? false
        let finalImage: CGImage
        if !sourceHasAlpha,
           cgImage.alphaInfo != .noneSkipLast,
           cgImage.alphaInfo != .noneSkipFirst,
           cgImage.alphaInfo != .none {
            if let context = CGContext(
                data: nil,
                width: cgImage.width,
                height: cgImage.height,
                bitsPerComponent: 8,
                bytesPerRow: 0,
                space: cgImage.colorSpace ?? CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue
            ) {
                context.draw(cgImage, in: CGRect(x: 0, y: 0, width: cgImage.width, height: cgImage.height))
                finalImage = context.makeImage() ?? cgImage
            } else {
                finalImage = cgImage
            }
        } else {
            finalImage = cgImage
        }

        // Center-crop portrait/narrow images to the screen's aspect ratio.
        // macOS wallpaper scaling fills the screen height and clips horizontal overflow,
        // so images narrower than the screen produce pillar-box bars. Cropping the image
        // height to match the screen aspect ratio ensures it fills wall-to-wall.
        let screenAspect = targetAspectRatio()
        let imgAspect = CGFloat(finalImage.width) / CGFloat(finalImage.height)
        let imageToEncode: CGImage
        if imgAspect < screenAspect - 0.05 {
            let cropHeight = Int((CGFloat(finalImage.width) / screenAspect).rounded())
            let yOffset = (finalImage.height - cropHeight) / 2
            if cropHeight > 0, yOffset >= 0,
               let cropped = finalImage.cropping(to: CGRect(
                   x: 0, y: yOffset,
                   width: finalImage.width, height: cropHeight
               )) {
                imageToEncode = cropped
            } else {
                imageToEncode = finalImage
            }
        } else {
            imageToEncode = finalImage
        }

        // Encode
        let outputType: CFString
        let quality: CGFloat
        if heicSupported {
            outputType = UTType.heic.identifier as CFString
            quality = heicQuality
        } else {
            outputType = UTType.jpeg.identifier as CFString
            quality = 0.82
        }

        guard let destination = CGImageDestinationCreateWithURL(
            outputURL as CFURL,
            outputType,
            1, nil
        ) else {
            try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
            return destinationURL
        }

        let destOptions: [CFString: Any] = [
            kCGImageDestinationLossyCompressionQuality: quality,
            kCGImageDestinationEmbedThumbnail: true
        ]
        CGImageDestinationAddImage(destination, imageToEncode, destOptions as CFDictionary)

        guard CGImageDestinationFinalize(destination) else {
            try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
            return destinationURL
        }

        return outputURL
    }

    // MARK: - Screen Resolution

    func targetMaxPixelDimension() -> CGFloat {
        DispatchQueue.main.sync {
            NSScreen.screens.map { screen in
                let backingRect = screen.convertRectToBacking(screen.frame)
                return max(backingRect.width, backingRect.height)
            }.max() ?? 3840
        }
    }

    /// Returns the widest screen's aspect ratio (width/height) in backing pixels.
    /// Used to center-crop narrow images so they fill the screen without pillar-box bars.
    func targetAspectRatio() -> CGFloat {
        DispatchQueue.main.sync {
            NSScreen.screens.map { screen in
                let r = screen.convertRectToBacking(screen.frame)
                return r.width / r.height
            }.max() ?? (16.0 / 9.0)
        }
    }
}
