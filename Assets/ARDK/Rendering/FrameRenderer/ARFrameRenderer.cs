// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Camera;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Niantic.ARDK.Rendering
{
  /// Rendering logic and resources shared across all platforms.
  public abstract class ARFrameRenderer:
    IARFrameRenderer,
    IDisposable
  {
#region IARFrameRenderer Overrides
    public RenderTarget Target { get; }
    public Resolution Resolution { get; }
    public Matrix4x4 DisplayTransform { get; private set; }
    public Matrix4x4 ProjectionTransform { get; private set; }

    public float NearPlane
    {
      get
      {
        return _nearClipPlane;
      }
      set
      {
        _nearClipPlane = value;
        if (Target.IsTargetingCamera)
          Target.Camera.nearClipPlane = value;
      }
    }

    public float FarPlane
    {
      get
      {
        return _farClipPlane;
      }
      set
      {
        _farClipPlane = value;
        if (Target.IsTargetingCamera)
          Target.Camera.farClipPlane = value;
      }
    }

    public GraphicsFence? GPUFence { get; protected set; }

    public virtual int RecommendedFrameRate
    {
      get
      {
        return 60;
      }
    }

    /// Event for when the renderer had just initialized.
    public event ArdkEventHandler<FrameRenderedArgs> Initialized
    {
      add
      {
        _initialized += value;
        if (_isInitialized)
        {
          var args = new FrameRenderedArgs(this);
          value.Invoke(args);
        }
      }
      remove => _initialized -= value;
    }
    private event ArdkEventHandler<FrameRenderedArgs> _initialized;
    
    /// Event for when the renderer had just finished rendering to its primary target.
    public event ArdkEventHandler<FrameRenderedArgs> FrameRendered;
    
    public void AddFeatureProvider(IRenderFeatureProvider provider)
    {
      if (!_isInitialized)
        throw new InvalidOperationException
        (
          "Cannot add IRenderStateProvider components. The renderer has not yet been initialized."
        );

      if (_stateProviders.Contains(provider))
      {
        ARLog._Warn("The specified render state provider is already attached to the renderer.");
        return;
      }

      var features = provider.Features;
      if (_stateProviders.Any(entry => entry.Features.Overlaps(features)))
        throw new InvalidOperationException("Tried to add a conflicting IRenderStateProvider instance.");

      // Listen to when the provider enables or disables render features
      provider.ActiveFeaturesChanged += ActiveFeaturesChanged;

      // Assign render target
      provider.Target = Target;

        // Register the provider
      _stateProviders.Add(provider);
    }

    public void RemoveFeatureProvider(IRenderFeatureProvider provider)
    {
      if (!_isInitialized)
        return;

      if (!_stateProviders.Contains(provider))
      {
        ARLog._Warn("The specified render state provider is not attached to the renderer.");
        return;
      }

      provider.ActiveFeaturesChanged -= ActiveFeaturesChanged;
      _stateProviders.Remove(provider);
    }
#endregion

#region Abstraction Layer
    /// The platform specific shader used to render the frame.
    protected abstract Shader Shader { get; }

    [Obsolete("Implement OnConfigurePipeline() override without Resolution params instead.")]
    protected virtual GraphicsFence? OnConfigurePipeline
    (
      RenderTarget target,
      Resolution targetResolution,
      Resolution sourceResolution,
      Material renderMaterial
    )
    {
      // Do nothing
      return null;
    }

    /// Invoked when it is time to allocate rendering resources
    /// and configure the platform specific pipeline.
    /// @param target The specified render target.
    /// @param renderMaterial Material that will be used for rendering.
    /// @returns Fence that should be waited on in other command buffers that utilize the
    ///          texture output by this renderer.
    protected abstract GraphicsFence? OnConfigurePipeline
    (
      RenderTarget target,
      Material renderMaterial
    );

    /// Invoked when it is time to update the renderer's internal state before rendering.
    /// This call is required to populate the provided material's properties with
    /// information from the ARFrame.
    /// @param frame Frame to render.
    /// @param projectionTransform Projection matrix.
    /// @param displayTransform Affine transform to properly present the image on the screen.
    /// @param material The material that will be used to render the frame.
    /// @returns Whether the provided information was sufficient to render the frame.
    protected abstract bool OnUpdateState
    (
      IARFrame frame,
      Matrix4x4 projectionTransform,
      Matrix4x4 displayTransform,
      Material material
    );

    /// Invoked if it is time to add any command buffers to the camera.
    protected abstract void OnAddToCamera(Camera camera);

    /// Invoked if it is time to remove any command buffers to the camera.
    protected abstract void OnRemoveFromCamera(Camera camera);

    /// Invoked if it is time to manually perform the GPU commands to render the frame.
    protected abstract void OnIssueCommands();

    /// Invoked when this renderer is about to be deallocated.
    protected abstract void OnRelease();
#endregion

    // State variables...
    private bool _isInitialized;

    public bool IsEnabled { get; private set; }

    // Clipping plane distances for projection
    private float _nearClipPlane = 0.1f;
    private float _farClipPlane = 100.0f;

    // Cached initial screen orientation
    private readonly ScreenOrientation _originalOrientation;

    // Cache the current orientation to update native display geometry when it changes
#pragma warning disable 0219
    private ScreenOrientation _currentOrientation = ScreenOrientation.AutoRotation;
#pragma warning restore 0219

    /// Material used to render the frame.
    private Material _renderMaterial;

    /// Whether the renderer is allowed to rotate the image.
    public bool IsOrientationLocked = false;

    // A collection of the components that provide additional
    // information to render an AR Frame.
    private readonly List<IRenderFeatureProvider> _stateProviders = new List<IRenderFeatureProvider>();

    /// Allocates a new ARFrameRenderer.
    /// @param target The render target AR frames will be rendered to.
    ///               If this is a camera, its projection matrix will
    ///               automatically get updated for each frame.
    protected ARFrameRenderer(RenderTarget target)
    {
      _originalOrientation = MathUtils.CalculateScreenOrientation();

      Target = target;
      Resolution = target.GetResolution(_originalOrientation);

      if (target.IsTargetingCamera)
      {
        // Set frustum to match the camera
        var camera = target.Camera;
        _nearClipPlane = camera.nearClipPlane;
        _farClipPlane = camera.farClipPlane;
      }
    }

    /// Allocates a new ARFrameRenderer using specified frustum planes.
    /// @param target The render target AR frames will be rendered to.
    ///               If this is a camera, its projection matrix will
    ///               automatically get updated for each frame.
    /// @param near The distance of the near clipping plane.
    /// @param far The distance of the far clipping plane.
    protected ARFrameRenderer(RenderTarget target, float near, float far)
    {
      _originalOrientation = MathUtils.CalculateScreenOrientation();
      
      Target = target;
      Resolution = target.GetResolution(_originalOrientation);
      NearPlane = near;
      FarPlane = far;
    }

    /// Initializes rendering resources.
    /// @note This needs to be called before any frame is sent to this renderer to process.
    public void Initialize()
    {
      if (_isInitialized)
      {
        ARLog._Error("The ARFrameRenderer is already initialized");
        return;
      }

      // Acquire the platform specific shader
      Shader platformShader = Shader;
      Assert.IsNotNull(platformShader, "platformShader != null");

      // Allocate the frame rendering material
      _renderMaterial = new Material(platformShader);
      
      // Perform platform specific configurations
      ConfigurePipeline(Target, _renderMaterial);

      _initialized?.Invoke(new FrameRenderedArgs(this));
      _isInitialized = true;
    }

    /// Enables the renderer. If the renderer is targeting a camera,
    /// this will attach the execution of the rendering commands to it.
    public void Enable()
    {
      if (IsEnabled)
        return;

      // Cache state
      IsEnabled = true;

      // Wait for the pipeline to initialize
      if (!_isInitialized)
        return;

      if (Target.IsTargetingCamera)
      {
        RegisterCameraEvents();
        OnAddToCamera(Target.Camera);
      }
    }

    /// Disables the renderer. The rendering commands will be
    /// detached from the target camera, if any.
    public void Disable()
    {
      if (!IsEnabled)
        return;

      // Cache state
      IsEnabled = false;

      // Wait for the pipeline to initialize
      if (!_isInitialized)
        return;

      if (Target.IsTargetingCamera)
      {
        UnregisterCameraEvents();

        if (Target.Camera)
        {
          OnRemoveFromCamera(Target.Camera);
          Target.Camera.ResetProjectionMatrix();
        }
      }
    }

    private void RegisterCameraEvents()
    {
#if ARDK_HAS_URP
      RenderPipelineManager.endCameraRendering += RenderPipelineManager_OnEndCameraRendering;
#else
      Camera.onPostRender += Camera_OnPostRender;
#endif
    }

    private void UnregisterCameraEvents()
    {
#if ARDK_HAS_URP
      RenderPipelineManager.endCameraRendering -= RenderPipelineManager_OnEndCameraRendering;
#else
      Camera.onPostRender -= Camera_OnPostRender;
#endif
    }

    private void ConfigurePipeline
    (
      RenderTarget target,
      Material renderMaterial
    )
    {
#pragma warning disable 0618
      // Set up the platform specific commands
      // TODO: Remove call to deprecated function when next major release lands
      GPUFence = OnConfigurePipeline  
      (
        target,
        target.GetResolution(MathUtils.CalculateScreenOrientation()),
        new Resolution(),
        renderMaterial
      ) ?? OnConfigurePipeline(target, renderMaterial);
#pragma warning restore 0618
      
      // If this renderer is already enabled by the time it
      // initialized, hook up to the target camera's loop
      if (IsEnabled && Target.IsTargetingCamera)
      {
        RegisterCameraEvents();
        OnAddToCamera(Target.Camera);
      }
    }

    public void UpdateState(IARFrame withFrame)
    {
      if (!_isInitialized || !IsEnabled)
        return;
      
      if (withFrame?.Camera == null || withFrame.CapturedImageTextures == null)
        return;

      // Calculate camera matrices
      UpdateCameraMatrices(withFrame);

      // Update states from the ARFrame
      var didUpdateState = OnUpdateState
      (
        withFrame,
        ProjectionTransform,
        DisplayTransform,
        _renderMaterial
      );

      if (!didUpdateState)
        return;

      // Update states from additional providers
      for (var i = 0; i < _stateProviders.Count; i++)
        _stateProviders[i].UpdateRenderState(_renderMaterial);

      // in case we're rendering offscreen...
      if (!Target.IsTargetingCamera)
      {
        // ... execute the command buffer now
        OnIssueCommands();

        // Propagate event
        FrameRendered?.Invoke(new FrameRenderedArgs(this));
      }
      else
      {
        // In case we are targeting a camera, update its projection
        Target.Camera.projectionMatrix = ProjectionTransform;
      }
    }

    /// Performs a graphics blit to the specified texture using the renderer's current
    /// internal state. To update the renderer's state, use the UpdateState(IARFrame) API.
    /// @param texture The render target. This texture needs to be already allocated.
    public void BlitToTexture(ref RenderTexture texture)
    {
      if (!_isInitialized)
        throw new InvalidOperationException
          ("The ARFrameRenderer component cannot blit to texture in its current state");

      if (texture == null)
        return;
      
      var prev = RenderTexture.active;
      Graphics.Blit(null, texture, _renderMaterial);
      RenderTexture.active = prev;
    }

#if ARDK_HAS_URP

    private void RenderPipelineManager_OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
      // Propagate event
      if (Target.Camera == cam)
        FrameRendered?.Invoke(new FrameRenderedArgs(this));
    }

#else

    private void Camera_OnPostRender(Camera cam)
    {
      // Propagate event
      if (Target.Camera == cam)
        FrameRendered?.Invoke(new FrameRenderedArgs(this));
    }

#endif

    private void ActiveFeaturesChanged(RenderFeaturesChangedArgs args)
    {
      var featuresEnabled = args.Configuration.FeaturesEnabled;
      if (featuresEnabled != null)
      {
        foreach (var feature in featuresEnabled)
        {
          ARLog._Debug("Enable: " + feature);
          _renderMaterial.EnableKeyword(feature);
        }
      }

      var featuresDisabled = args.Configuration.FeaturesDisabled;
      if (featuresDisabled != null)
      {
        foreach (var feature in featuresDisabled)
        {
          ARLog._Debug("Disable: " + feature);
          _renderMaterial.DisableKeyword(feature);
        }
      }
    }

    private void Release()
    {
      OnRelease();

      if (_renderMaterial != null)
        Object.Destroy(_renderMaterial);
    }

    public void Dispose()
    {
      // Disable renderer
      Disable();

      // Remove render state providers
      foreach (var provider in _stateProviders)
        provider.ActiveFeaturesChanged -= ActiveFeaturesChanged;

      _stateProviders.Clear();

      Release();
      GC.SuppressFinalize(this);
    }

    ~ARFrameRenderer()
    {
      Release();
    }

#region Utility Methods
    protected static void CreateOrUpdateExternalTexture
    (
      ref Texture2D texture,
      Resolution resolution,
      TextureFormat format,
      IntPtr nativeHandle
    )
    {
      if (texture == null)
      {
        texture = Texture2D.CreateExternalTexture
        (
          resolution.width,
          resolution.height,
          format,
          false,
          false,
          nativeHandle
        );
      }
      else
      {
        texture.UpdateExternalTexture(nativeHandle);
      }
    }

    private void UpdateCameraMatrices(IARFrame frame)
    {
      // Determine target orientation
      var targetOrientation =
        IsOrientationLocked
          ? _originalOrientation
          : MathUtils.CalculateScreenOrientation();

      // Calculate the target resolution according to the orientation
      var targetResolution = Target.GetResolution(targetOrientation);
      // Before getting any of the matrices, we need to call CorrectToActualOrientation() make sure
      // C++ display geometry is updated on Android.
      CorrectToActualOrientation(frame, targetOrientation, targetResolution.width, targetResolution.height);

      // Update the projection matrix
      ProjectionTransform = frame.Camera.CalculateProjectionMatrix
      (
        targetOrientation,
        targetResolution.width,
        targetResolution.height,
        _nearClipPlane,
        _farClipPlane
      );

      // Update the display transform matrix
      DisplayTransform = frame.CalculateDisplayTransform
      (
        targetOrientation,
        targetResolution.width,
        targetResolution.height
      );
    }

    private void CorrectToActualOrientation
    (
      IARFrame frame,
      ScreenOrientation orientation,
      int screenWith,
      int screenHeight
    )
    {
      if (frame.Camera is IUpdatableARCamera updatableCamera)
      {
        if (_currentOrientation != orientation)
        {
          updatableCamera.UpdateDisplayGeometry(orientation, screenWith, screenHeight);
          _currentOrientation = orientation;
        }
      }
    }
    #endregion
  }
}
