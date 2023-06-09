﻿Shader "Unlit/ARKitFrame"
{
    Properties
    {
        _textureY ("TextureY", 2D) = "black" {}
        _textureCbCr ("TextureCbCr", 2D) = "gray" {}
        _textureDepth ("Depth", 2D) = "white" {}
        _textureFusedDepth ("Fused Depth", 2D) = "white" {}
        _textureDepthSuppressionMask ("Depth Suppresion Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Cull Off
            ZTest Always
            ZWrite On
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local __ DEPTH_ZWRITE
            #pragma multi_compile_local __ DEPTH_COMPRESSION
            #pragma multi_compile_local __ DEPTH_SUPPRESSION
            #pragma multi_compile_local __ DEPTH_STABILIZATION
            #pragma multi_compile_local __ DEPTH_DEBUG

            #include "UnityCG.cginc"

            // Transformation used to convert yCbCr color format to RGB
            static const float4x4 colorTransform = float4x4(
                float4(1.0, +0.0000, +1.4020, -0.7010),
                float4(1.0, -0.3441, -0.7141, +0.5291),
                float4(1.0, +1.7720, +0.0000, -0.8860),
                float4(0.0, +0.0000, +0.0000, +1.0000)
            );

            // Transform used to sample the color planes
            float4x4 _displayTransform;

            // Transforms used to sample the context awareness textures
            float4x4 _depthTransform;
            float4x4 _semanticsTransform;

            // Used to linearize values of the depth texture
            float4 _DepthBufferParams;

            // Plane samplers
            sampler2D _textureY;
            sampler2D _textureCbCr;
            sampler2D _textureDepth;
            sampler2D _textureFusedDepth;
            sampler2D _textureDepthSuppressionMask;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 color_uv : TEXCOORD0;
#if DEPTH_ZWRITE
                float3 depth_uv : TEXCOORD1;
#if DEPTH_SUPPRESSION
                float3 semantics_uv : TEXCOORD2;
#endif
#if DEPTH_STABILIZATION
                float2 vertex_uv : TEXCOORD3;
#endif
#endif
            };

#if DEPTH_ZWRITE

            // Inverse of LinearEyeDepth
            inline float EyeDepthToNonLinear(float eyeDepth, float4 zBufferParam)
            {
	            return (1.0f - (eyeDepth * zBufferParam.w)) / (eyeDepth * zBufferParam.z * 2.0f);
            }

            // Linearizes depth
            inline float LinearEyeDepth(float z, float4 zparams)
            {
              return 1.0f / (zparams.z * z + zparams.w);
            }

#endif

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Apply display transform
                o.color_uv = mul(_displayTransform, float4(v.uv, 0.0f, 1.0f)).xy;

                // Transform UVs for the context awareness textures
#if DEPTH_ZWRITE
                o.depth_uv = mul(_depthTransform, float4(v.uv, 1.0f, 1.0f)).xyz;
#if DEPTH_SUPPRESSION
                o.semantics_uv = mul(_semanticsTransform, float4(v.uv, 1.0f, 1.0f)).xyz;
#endif
#if DEPTH_STABILIZATION
                o.vertex_uv = v.uv;
#endif
#endif

                return o;
            }

            void frag(in v2f i, out float4 out_color : SV_Target, out float out_depth : SV_Depth)
            {
                // Convert the biplanar image to RGB
                out_color = mul(colorTransform,
                    float4(tex2D(_textureY, i.color_uv).r, tex2D(_textureCbCr, i.color_uv).rg, 1.0f));

#if !UNITY_COLORSPACE_GAMMA
                out_color.xyz = GammaToLinearSpace(out_color.xyz);
#endif

                // Clear depth
                out_depth = 0.0f;

#if DEPTH_ZWRITE
#if DEPTH_SUPPRESSION
                // If depth is not suppressed at this pixel
                float2 semanticsUV = float2(i.semantics_uv.x / i.semantics_uv.z, i.semantics_uv.y / i.semantics_uv.z);
                if (tex2D(_textureDepthSuppressionMask, semanticsUV).r == 0.0f)
#endif
                {
                    // Sample depth
                    float2 depthUV = float2(i.depth_uv.x / i.depth_uv.z, i.depth_uv.y / i.depth_uv.z);
                    float rawDepth = tex2D(_textureDepth, depthUV).r;

#if DEPTH_COMPRESSION
                    // In case of ARGB textures, the depth value is compressed to 8 bits, nonlinear
                    // Here, we convert this value to linear eye depth
                    float eyeDepth = LinearEyeDepth(rawDepth, _DepthBufferParams);
#else
                    float eyeDepth = rawDepth;
#endif

#if DEPTH_STABILIZATION
                    // Calculate non-linear frame depth
                    float frameDepth = EyeDepthToNonLinear(eyeDepth, _ZBufferParams);

                    // Sample non-linear fused depth
                    float fusedDepth = tex2D(_textureFusedDepth, i.vertex_uv).r;

                    // Linearize and compare
                    float frameLinear = Linear01Depth(frameDepth);
                    float fusedLinear = Linear01Depth(fusedDepth);
                    bool useFrameDepth = fusedLinear == 1 || (abs(fusedLinear - frameLinear) / fusedLinear) >= 0.5f;

                    // Write z-buffer
                    out_depth = useFrameDepth ? frameDepth : fusedDepth;

#else
                    // Convert to nonlinear and write to the zbuffer
                    out_depth = EyeDepthToNonLinear(eyeDepth, _ZBufferParams);
#endif
#if DEPTH_DEBUG
                    // Write disparity to the color channels for debug purposes
                    const float MAX_VIEW_DISP = 4.0f;
                    const float scaledDisparity = 1.0f / LinearEyeDepth(out_depth);
                    const float normDisparity = scaledDisparity/MAX_VIEW_DISP;
                    out_color = float4(normDisparity, normDisparity, normDisparity, 1.0f);

#if DEPTH_STABILIZATION
                    if (useFrameDepth)
                        out_color = float4(normDisparity, normDisparity * 0.5f, normDisparity, 1.0f);
#endif
#endif

                }
#endif
            }
            ENDHLSL
        }
    }
}
