#ifndef MASK_SDF_H
#define MASK_SDF_H

#include "../../Shared/Shaders/Includes/GenerationContext.hlsl"

// --- Helper Functions ---
float sdBox(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
}   

// Signed Distance to an Ellipsoid
float sdEllipsoid(float3 p, float3 r) {
    float k0 = length(p/r);
    float k1 = length(p/(r*r));
    return k0 * (k0 - 1.0) / max(k1, 0.0001);
}

// --- Mask Logic ---

float GetMaskSDF(float3 pos) {
    float3 p = pos;
    // --- SHAPE COMPOSITION (FLAT MASK) ---

    // A. The Cranium (Forehead/Top of head)
    // Flattened: Z Radius reduced from 20.0 to 4.0
    float3 headCenter = p - float3(0.0, 6.0, 0.0);
    float dCranium = sdEllipsoid(headCenter, float3(22.0, 22.0, 4.0));

    // B. The Jaw (Chin/Bottom of face)
    // Flattened: Z Radius reduced from 18.0 to 4.0, Z Offset centered at 0
    float3 jawCenter = p - float3(0.0, -12.0, 0.0);
    float dJaw = sdEllipsoid(jawCenter, float3(16.0, 20.0, 4.0));

    // Blend Cranium and Jaw
    float hFace;
    float dFace = smin(dCranium, dJaw, 12.0, hFace);

    // C. The Nose Bridge
    // Flattened: Z Radius reduced from 6.0 to 4.0, Z Offset centered at 0
    // (Note: This blends the nose flat into the face. To make it protrude slightly, increase Z radius to ~5.0 or 6.0)
    float3 nosePos = p - float3(0.0, -2.0, 0.0);
    float dNose = sdEllipsoid(nosePos, float3(5.0, 10.0, 4.0));

    // Blend Nose into Face
    float hNose;
    float dBaseShape = smin(dFace, dNose, 6.0, hNose);

    return dBaseShape;
}

void Stage_Mask(inout GenerationContext ctx) {
    float3 p = ctx.position;
    
    float d = GetMaskSDF(p);

    if (d < ctx.sdf) {
        ctx.sdf = d;
        ctx.material = 1; // Mask Material
        
        // Gradient calculation
        float e = 0.1;
        float v1 = GetMaskSDF(p + float3(e, 0, 0));
        float v2 = GetMaskSDF(p - float3(e, 0, 0));
        float v3 = GetMaskSDF(p + float3(0, e, 0));
        float v4 = GetMaskSDF(p - float3(0, e, 0));
        float v5 = GetMaskSDF(p + float3(0, 0, e));
        float v6 = GetMaskSDF(p - float3(0, 0, e));
        ctx.gradient = normalize(float3(v1-v2, v3-v4, v5-v6));
    }
}

#endif