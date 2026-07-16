Shader "Wayfu/Stencil/Diffuse NotEqualOne"
{
	Properties
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		[MainTexture] _MainTex ("Base (RGB)", 2D) = "white" {}
		[IntRange] _StencilRef ("Stencil Ref", Range(0, 255)) = 2
		[IntRange] _StencilReadMask ("Stencil Read Mask", Range(0, 255)) = 2
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}
		LOD 200

		Stencil
		{
			Ref [_StencilRef]
			ReadMask [_StencilReadMask]
			Comp NotEqual
			Pass Keep
		}

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		TEXTURE2D(_MainTex);
		SAMPLER(sampler_MainTex);

		CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			half4 _Color;
			float _StencilRef;
			float _StencilReadMask;
		CBUFFER_END
		ENDHLSL

		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex Vertex
			#pragma fragment Fragment

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float2 uv         : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv         : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				half3  normalWS   : TEXCOORD2;
				half   fogCoord   : TEXCOORD3;
			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord : TEXCOORD4;
			#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings Vertex(Attributes input)
			{
				Varyings output = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
				VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

				output.positionCS = positionInputs.positionCS;
				output.positionWS = positionInputs.positionWS;
				output.normalWS = normalInputs.normalWS;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				output.shadowCoord = GetShadowCoord(positionInputs);
			#endif

				return output;
			}

			half4 Fragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				half3 albedo = texColor.rgb * _Color.rgb;
				half alpha = texColor.a * _Color.a;

				float3 positionWS = input.positionWS;
				half3 normalWS = normalize(input.normalWS);

			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord = input.shadowCoord;
			#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
				float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
			#else
				float4 shadowCoord = float4(0, 0, 0, 0);
			#endif

				half4 shadowMask = unity_ProbesOcclusion;
				Light mainLight = GetMainLight(shadowCoord, positionWS, shadowMask);

				half3 lighting = LightingLambert(mainLight.color, mainLight.direction, normalWS)
					* (mainLight.shadowAttenuation * mainLight.distanceAttenuation);

			#ifdef _ADDITIONAL_LIGHTS
				uint additionalLightCount = GetAdditionalLightsCount();
				for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
				{
					Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
					lighting += LightingLambert(light.color, light.direction, normalWS)
						* (light.shadowAttenuation * light.distanceAttenuation);
				}
			#endif

				half3 color = albedo * (lighting + SampleSH(normalWS));
				color = MixFog(color, input.fogCoord);

				return half4(color, alpha);
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			ColorMask 0

			HLSLPROGRAM
			#pragma target 2.0
			#pragma vertex ShadowVertex
			#pragma fragment ShadowFragment
			#pragma multi_compile_instancing
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			float3 _LightDirection;
			float3 _LightPosition;

			struct ShadowAttributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct ShadowVaryings
			{
				float4 positionCS : SV_POSITION;
			};

			ShadowVaryings ShadowVertex(ShadowAttributes input)
			{
				ShadowVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);

				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

			#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize(_LightPosition - positionWS);
			#else
				float3 lightDirectionWS = _LightDirection;
			#endif

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
			#if UNITY_REVERSED_Z
				positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
			#else
				positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
			#endif

				output.positionCS = positionCS;
				return output;
			}

			half4 ShadowFragment(ShadowVaryings input) : SV_Target
			{
				return 0;
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }

			ZWrite On
			ColorMask R

			HLSLPROGRAM
			#pragma target 2.0
			#pragma vertex DepthVertex
			#pragma fragment DepthFragment
			#pragma multi_compile_instancing

			struct DepthAttributes
			{
				float4 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct DepthVaryings
			{
				float4 positionCS : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			DepthVaryings DepthVertex(DepthAttributes input)
			{
				DepthVaryings output = (DepthVaryings)0;

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				return output;
			}

			half4 DepthFragment(DepthVaryings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				return 0;
			}
			ENDHLSL
		}
	}

	FallBack "Universal Render Pipeline/Lit"
}
