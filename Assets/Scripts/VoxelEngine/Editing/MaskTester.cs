using UnityEngine;
using VoxelEngine.Editing;

[CreateAssetMenu(fileName = "MaskTester", menuName = "VoxelEngine/MaskTester")]
public class MaskTester : ScriptableObjectSingleton<MaskTester>
{
    public Texture2D[] referenceImages;

    [VInspector.Button]
    public void TestImage(int imageNum = 0)
    {
        if (referenceImages == null || imageNum < 0 || imageNum >= referenceImages.Length)
        {
            Debug.LogError($"Invalid image number: {imageNum}. Reference array size: {(referenceImages != null ? referenceImages.Length : 0)}");
            return;
        }

        Texture2D refImg = referenceImages[imageNum];
        if (refImg == null)
        {
            Debug.LogError($"Reference image at index {imageNum} is null!");
            return;
        }

        VoxelProjectionCapture captureTool = VoxelProjectionCapture.Instance;
        if (captureTool == null)
        {
            // Fallback: Try finding it
            captureTool = FindAnyObjectByType<VoxelProjectionCapture>();
            if (captureTool == null)
            {
                Debug.LogError("VoxelProjectionCapture not found in scene!");
                return;
            }
        }

        // Force resolution match? Or just warn?
        // Ideally, we temporarily set the capture resolution to match the reference image.
        int originalRes = captureTool.resolution;
        captureTool.resolution = refImg.width; // Assuming square
        
        Texture2D genImg = captureTool.CaptureToTexture();
        
        // Restore resolution
        captureTool.resolution = originalRes;

        if (genImg == null)
        {
            Debug.LogError("Failed to generate capture image.");
            return;
        }

        if (genImg.width != refImg.width || genImg.height != refImg.height)
        {
            Debug.LogError($"Resolution mismatch! Ref: {refImg.width}x{refImg.height}, Gen: {genImg.width}x{genImg.height}. Capture resolution was set to match, but something failed.");
            DestroyImmediate(genImg);
            return;
        }

        int width = genImg.width;
        int height = genImg.height;
        Color[] refPixels = refImg.GetPixels();
        Color[] genPixels = genImg.GetPixels();
        
        int matchCount = 0;
        int totalPixels = width * height;

        for (int i = 0; i < totalPixels; i++)
        {
            // Reference: White (1) = Hollow, Black (0) = Solid
            // Generated: White (1) = Solid, Black (0) = Hollow
            // So: Match if Ref (Hollow) corresponds to Gen (Hollow) -> Ref=1, Gen=0
            //     Match if Ref (Solid) corresponds to Gen (Solid)   -> Ref=0, Gen=1
            
            // Using Red channel for comparison as images are B&W
            float rRef = refPixels[i].r;
            float rGen = genPixels[i].r;

            // Simple thresholding
            bool refIsHollow = rRef > 0.5f;
            bool genIsHollow = rGen < 0.5f; 

            if (refIsHollow == genIsHollow)
            {
                matchCount++;
            }
        }

        float percentage = (float)matchCount / totalPixels * 100.0f;
        Debug.Log($"Image Comparison Result ({imageNum}): {percentage:F2}% match.");

        // Clean up
        if (Application.isPlaying) Destroy(genImg);
        else DestroyImmediate(genImg);
    }
}
