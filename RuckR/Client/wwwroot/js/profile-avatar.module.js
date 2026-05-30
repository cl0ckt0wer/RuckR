const allowedTypes = new Set(["image/jpeg", "image/png", "image/webp"]);

export async function resizeSelectedProfileAvatar(inputId, options) {
    const input = document.getElementById(inputId);
    const file = input?.files?.[0];

    if (!file) {
        throw new Error("Choose an image file.");
    }

    if (!allowedTypes.has(file.type)) {
        throw new Error("Profile picture must be a JPEG, PNG, or WebP image.");
    }

    if (file.size <= 0) {
        throw new Error("Choose a non-empty image file.");
    }

    if (file.size > options.maxSourceBytes) {
        throw new Error(`Profile picture source must be ${formatBytes(options.maxSourceBytes)} or smaller.`);
    }

    const decoded = await decodeImage(file);
    try {
        const target = calculateTargetSize(decoded.width, decoded.height, options.maxEdgePixels);
        const output = await encodeBestFit(
            decoded.source,
            target,
            options.quality,
            options.maxOutputBytes);

        return {
            dataUrl: await blobToDataUrl(output.blob),
            contentType: output.blob.type,
            fileName: replaceExtension(file.name, output.blob.type),
            originalSize: file.size,
            outputSize: output.blob.size,
            originalWidth: decoded.width,
            originalHeight: decoded.height,
            width: output.width,
            height: output.height,
            wasResized: output.width !== decoded.width
                || output.height !== decoded.height
                || output.blob.type !== file.type
                || output.blob.size !== file.size
        };
    } finally {
        decoded.close();
    }
}

async function decodeImage(file) {
    if ("createImageBitmap" in window) {
        try {
            const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
            return {
                source: bitmap,
                width: bitmap.width,
                height: bitmap.height,
                close: () => bitmap.close?.()
            };
        } catch {
            // Fall through to the Image element path for browsers or images that
            // do not support createImageBitmap for this file.
        }
    }

    const dataUrl = await blobToDataUrl(file);
    const image = await loadImage(dataUrl);
    return {
        source: image,
        width: image.naturalWidth || image.width,
        height: image.naturalHeight || image.height,
        close: () => {
        }
    };
}

function calculateTargetSize(width, height, maxEdge) {
    if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) {
        throw new Error("Profile picture dimensions could not be read.");
    }

    const scale = Math.min(1, maxEdge / Math.max(width, height));
    return {
        width: Math.max(1, Math.round(width * scale)),
        height: Math.max(1, Math.round(height * scale))
    };
}

async function encodeBestFit(source, target, quality, maxOutputBytes) {
    const supportsWebp = await canvasSupportsType("image/webp");
    const preferredType = supportsWebp ? "image/webp" : "image/jpeg";
    const attempts = [
        { width: target.width, height: target.height, quality },
        { width: target.width, height: target.height, quality: Math.min(quality, 0.72) },
        { width: Math.min(target.width, 640), height: Math.min(target.height, 640), quality: 0.78 },
        { width: Math.min(target.width, 512), height: Math.min(target.height, 512), quality: 0.78 },
        { width: Math.min(target.width, 384), height: Math.min(target.height, 384), quality: 0.72 }
    ];

    let lastBlob = null;
    for (const attempt of attempts) {
        const size = fitWithin(target.width, target.height, attempt.width, attempt.height);
        const blob = await renderToBlob(source, size, preferredType, attempt.quality);
        lastBlob = blob;
        if (blob.size <= maxOutputBytes) {
            return { blob, width: size.width, height: size.height };
        }
    }

    if (lastBlob) {
        throw new Error(`Profile picture could not be reduced below ${formatBytes(maxOutputBytes)}.`);
    }

    throw new Error("Profile picture could not be processed.");
}

function fitWithin(width, height, maxWidth, maxHeight) {
    const scale = Math.min(1, maxWidth / width, maxHeight / height);
    return {
        width: Math.max(1, Math.round(width * scale)),
        height: Math.max(1, Math.round(height * scale))
    };
}

async function renderToBlob(source, size, preferredType, quality) {
    const canvas = document.createElement("canvas");
    canvas.width = size.width;
    canvas.height = size.height;
    const context = canvas.getContext("2d", { alpha: preferredType !== "image/jpeg" });

    if (!context) {
        throw new Error("Profile picture could not be processed.");
    }

    if (preferredType === "image/jpeg") {
        context.fillStyle = "#fff";
        context.fillRect(0, 0, size.width, size.height);
    }

    context.imageSmoothingEnabled = true;
    context.imageSmoothingQuality = "high";
    context.drawImage(source, 0, 0, size.width, size.height);

    return await canvasToBlob(canvas, preferredType, quality)
        ?? await canvasToBlob(canvas, "image/jpeg", quality);
}

function canvasToBlob(canvas, type, quality) {
    return new Promise(resolve => canvas.toBlob(resolve, type, quality));
}

async function canvasSupportsType(type) {
    const canvas = document.createElement("canvas");
    canvas.width = 1;
    canvas.height = 1;
    const blob = await canvasToBlob(canvas, type, 0.8);
    return blob?.type === type;
}

function loadImage(dataUrl) {
    return new Promise((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve(image);
        image.onerror = () => reject(new Error("Profile picture could not be decoded."));
        image.src = dataUrl;
    });
}

function blobToDataUrl(blob) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(new Error("Profile picture could not be read."));
        reader.readAsDataURL(blob);
    });
}

function replaceExtension(fileName, contentType) {
    const extension = contentType === "image/webp"
        ? "webp"
        : contentType === "image/png"
            ? "png"
            : "jpg";
    const baseName = (fileName || "avatar")
        .replace(/\.[^.]+$/, "")
        .replace(/[^\w.-]+/g, "-")
        .replace(/^-+|-+$/g, "")
        .slice(0, 64) || "avatar";
    return `${baseName}.${extension}`;
}

function formatBytes(bytes) {
    if (bytes >= 1024 * 1024) {
        return `${Math.round(bytes / 1024 / 1024)} MB`;
    }

    return `${Math.round(bytes / 1024)} KB`;
}
