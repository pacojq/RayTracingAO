﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// the ray tracing tutorial interface.
/// </summary>
public abstract class RayTracingTutorial
{
  /// <summary>
  /// the camera shader parameters
  /// </summary>
  private static class CameraShaderParams
  {
    public static readonly int _WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
    public static readonly int _CameraViewProj = Shader.PropertyToID("_CameraViewProj");
    public static readonly int _InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
    public static readonly int _CameraFarDistance = Shader.PropertyToID("_CameraFarDistance");
    public static readonly int _ZBufferParams = Shader.PropertyToID("_ZBufferParams");
  }

  /// <summary>
  /// the tutorial asset.
  /// </summary>
  protected RayTracingTutorialAsset _asset;

  /// <summary>
  /// the render pipeline.
  /// </summary>
  protected RayTracingRenderPipeline _pipeline;

  /// <summary>
  /// the output target.
  /// </summary>
  private readonly Dictionary<int, RTHandle> _outputTargets = new Dictionary<int, RTHandle>();
  /// <summary>
  /// the output depths.
  /// </summary>
  private readonly Dictionary<int, RTHandle> _outputDepths = new Dictionary<int, RTHandle>();

  protected readonly int _outputTargetShaderId = Shader.PropertyToID("_OutputTarget");

  /// <summary>
  /// the output target size(width, height, 1/width, 1/height).
  /// </summary>
  private readonly Dictionary<int, Vector4> _outputTargetSizes = new Dictionary<int, Vector4>();
  protected readonly int _outputTargetSizeShaderId = Shader.PropertyToID("_OutputTargetSize");

  /// <summary>
  /// the shader.
  /// </summary>
  protected RayTracingShader _shader;

  /// <summary>
  /// constructor.
  /// </summary>
  /// <param name="asset">the tutorial asset.</param>
  protected RayTracingTutorial(RayTracingTutorialAsset asset)
  {
    _asset = asset;
  }

  /// <summary>
  /// initialize the tutorial.
  /// </summary>
  /// <param name="pipeline">the render pipeline.</param>
  /// <returns>the result.</returns>
  public virtual bool Init(RayTracingRenderPipeline pipeline)
  {
    _pipeline = pipeline;

    _shader = _asset.shader;

    return true;
  }

  /// <summary>
  /// render.
  /// </summary>
  /// <param name="context">the render context.</param>
  /// <param name="camera">the camera.</param>
  public virtual void Render(ScriptableRenderContext context, Camera camera)
  {
    SetupCamera(camera);
  }

  /// <summary>
  /// dispose.
  /// </summary>
  /// <param name="disposing">whether is disposing.</param>
  public virtual void Dispose(bool disposing)
  {
    foreach (var pair in _outputTargets)
    {
      RTHandles.Release(pair.Value);
    }
    _outputTargets.Clear();
    foreach (var pair in _outputDepths)
    {
      RTHandles.Release(pair.Value);
    }
    _outputDepths.Clear();
  }

  /// <summary>
  /// require a output target for camera.
  /// </summary>
  /// <param name="camera">the camera.</param>
  /// <param name="format">the output target format.</param>
  /// <returns>the output target.</returns>
  protected RTHandle RequireOutputTarget(Camera camera, GraphicsFormat format)
  {
    var id = camera.GetInstanceID();

    if (_outputTargets.TryGetValue(id, out var outputTarget))
      return outputTarget;

    outputTarget = RTHandles.Alloc(
      camera.pixelWidth,
      camera.pixelHeight,
      1,
      DepthBits.None,
      format,
      FilterMode.Bilinear,
      TextureWrapMode.Clamp,
      TextureDimension.Tex2D,
      true,
      false,
      false,
      false,
      1,
      0f,
      MSAASamples.None,
      false,
      false,
      RenderTextureMemoryless.None,
      $"OutputTarget_{camera.name}");
    _outputTargets.Add(id, outputTarget);

    return outputTarget;
  }

  /// <summary>
  /// require a output depth for camera.
  /// </summary>
  /// <param name="camera">the camera.</param>
  /// <returns>the output target.</returns>
  protected RTHandle RequireOutputDepth(Camera camera)
  {
    var id = camera.GetInstanceID();

    if (_outputDepths.TryGetValue(id, out var outputDepth))
      return outputDepth;

    outputDepth = RTHandles.Alloc(
      camera.pixelWidth,
      camera.pixelHeight,
      1,
      DepthBits.Depth32,
      GraphicsFormat.None,
      FilterMode.Point,
      TextureWrapMode.Clamp,
      TextureDimension.Tex2D,
      false,
      false,
      false,
      false,
      1,
      0f,
      MSAASamples.None,
      false,
      false,
      RenderTextureMemoryless.None,
      $"OutputDepth_{camera.name}");
    _outputDepths.Add(id, outputDepth);

    return outputDepth;
  }

  /// <summary>
  /// require a output target size for camera.
  /// </summary>
  /// <param name="camera">the camera.</param>
  /// <returns>the output target size.</returns>
  protected Vector4 RequireOutputTargetSize(Camera camera)
  {
    var id = camera.GetInstanceID();

    if (_outputTargetSizes.TryGetValue(id, out var outputTargetSize))
      return outputTargetSize;

    outputTargetSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
    _outputTargetSizes.Add(id, outputTargetSize);

    return outputTargetSize;
  }

  /// <summary>
  /// setup camera.
  /// </summary>
  /// <param name="camera">the camera.</param>
  private static void SetupCamera(Camera camera)
  {
    Shader.SetGlobalVector(CameraShaderParams._WorldSpaceCameraPos, camera.transform.position);
    var projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
    var viewMatrix = camera.worldToCameraMatrix;
    var viewProjMatrix = projMatrix * viewMatrix;
    var invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
    Shader.SetGlobalMatrix(CameraShaderParams._CameraViewProj, viewProjMatrix);
    Shader.SetGlobalMatrix(CameraShaderParams._InvCameraViewProj, invViewProjMatrix);
    Shader.SetGlobalFloat(CameraShaderParams._CameraFarDistance, camera.farClipPlane);

    var n = camera.nearClipPlane;
    var f = camera.farClipPlane;
    var scale     = projMatrix[2, 3] / (f * n) * (f - n);
    var reverseZ  = scale > 0;
    var zBufferParams = reverseZ ? new Vector4(-1 + f / n, 1, -1 / f + 1 / n, 1 / f) : new Vector4(1 - f / n, f / n, 1 / f - 1 / n, 1 / n);
    Shader.SetGlobalVector(CameraShaderParams._ZBufferParams, zBufferParams);
  }
}