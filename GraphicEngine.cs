using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.SPIRV;


namespace Blazor_Desktop
{
	public class GraphicEngine : IDisposable
	{
		public long FrameTime = 0;
		public long ConvertTime = 0;
		private DateTime? renderTime;

		private readonly GraphicsDevice _graphicsDevice;
		private CommandList _commandList;
		private DeviceBuffer _vertexBuffer;
		private DeviceBuffer _indexBuffer;
		private Framebuffer _offscreenFrameBuffer;
		private Shader[] _shaders;
		private Pipeline _pipeline;
		private const PixelFormat PIXEL_FORMAT = PixelFormat.B8_G8_R8_A8_UNorm;

		private const string VertexCode = @"
#version 450

layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}";

		private const string FragmentCode = @"
#version 450

layout(location = 0) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}";

		public GraphicEngine()
		{
			GraphicsDeviceOptions options = new GraphicsDeviceOptions
			{
				HasMainSwapchain = false,
				PreferStandardClipSpaceYDirection = true,
				PreferDepthRangeZeroToOne = true
			};

			_graphicsDevice = GraphicsDevice.CreateD3D11(options);
			CreateResources();
			Complete = true;
			//SaveScreenToFile();
		}

		private VertexPositionColor[] _quadVertices;

		private void CreateResources()
		{
			ResourceFactory factory = _graphicsDevice.ResourceFactory;

			_quadVertices = new[]
			{
				new VertexPositionColor(new Vector2(-1, 1), RgbaFloat.Red),
				new VertexPositionColor(new Vector2(1, 1), RgbaFloat.Green),
				new VertexPositionColor(new Vector2(-1, -1), RgbaFloat.Blue),
				new VertexPositionColor(new Vector2(1, -1), RgbaFloat.Yellow),
			};

			BufferDescription vbDescription = new BufferDescription(
				4 * VertexPositionColor.SizeInBytes,
				BufferUsage.VertexBuffer);
			_vertexBuffer = factory.CreateBuffer(vbDescription);
			_graphicsDevice.UpdateBuffer(_vertexBuffer, 0, _quadVertices);

			ushort[] quadIndices = {0, 1, 2, 3};
			BufferDescription ibDescription = new BufferDescription(
				4 * sizeof(ushort),
				BufferUsage.IndexBuffer);
			_indexBuffer = factory.CreateBuffer(ibDescription);
			_graphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);

			VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
				new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate,
					VertexElementFormat.Float2),
				new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate,
					VertexElementFormat.Float4));

			ShaderDescription vertexShaderDesc = new ShaderDescription(
				ShaderStages.Vertex,
				Encoding.UTF8.GetBytes(VertexCode),
				"main");
			ShaderDescription fragmentShaderDesc = new ShaderDescription(
				ShaderStages.Fragment,
				Encoding.UTF8.GetBytes(FragmentCode),
				"main");

			_shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

			var pixelFormat = PixelFormat.B8_G8_R8_A8_UNorm; // <- PixelFormat.B8_G8_R8_A8_UNorm, is it OK?

			var textureDescription =
				TextureDescription.Texture2D(800, 600, 1, 1, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.RenderTarget);
			textureDescription.Type = TextureType.Texture2D;
			textureDescription.Format = pixelFormat;

			var textureForRender = _graphicsDevice.ResourceFactory.CreateTexture(textureDescription);


			var framebufferDescription = new FramebufferDescription(null, textureForRender);
			_offscreenFrameBuffer = _graphicsDevice.ResourceFactory.CreateFramebuffer(framebufferDescription);

			CreatePipeline(vertexLayout, _offscreenFrameBuffer);
			
			_stage = _graphicsDevice.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
				textureForRender.Width,
				textureForRender.Height,
				1,
				1,
				PIXEL_FORMAT,
				TextureUsage.Staging));
			
			_commandList = factory.CreateCommandList();
			_commandList.SetFramebuffer(_offscreenFrameBuffer);
		}

		private Texture _stage;
		private void CreatePipeline(VertexLayoutDescription vertexLayout, Framebuffer frameBuffer)
		{
			GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
			pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
			pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
				depthTestEnabled: false,
				depthWriteEnabled: false,
				comparisonKind: ComparisonKind.LessEqual);
			pipelineDescription.RasterizerState = new RasterizerStateDescription(
				cullMode: FaceCullMode.Back,
				fillMode: PolygonFillMode.Solid,
				frontFace: FrontFace.Clockwise,
				depthClipEnabled: true,
				scissorTestEnabled: false);
			pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
			pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
			pipelineDescription.ShaderSet = new ShaderSetDescription(
				vertexLayouts: new VertexLayoutDescription[] {vertexLayout},
				shaders: _shaders);
			if (frameBuffer is object)
				pipelineDescription.Outputs = frameBuffer.OutputDescription;

			_pipeline = _graphicsDevice.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
		}

		private void RedrawContent()
		{
			if (renderTime is null)
				renderTime = DateTime.Now;
			var deltaTime = (DateTime.Now - renderTime).Value;
			var theta = ((float) deltaTime.Milliseconds / 1000) * Math.PI;
			for (int i = 0; i < _quadVertices.Length; i++)
			{
				var p = _quadVertices[i].Position;
				var x = Math.Clamp(p.X * (float)Math.Cos(theta) - p.Y * (float)Math.Sin(theta), -1, 1);
				var y = Math.Clamp(p.X * (float)Math.Sin(theta) + p.Y * (float)Math.Cos(theta), -1, 1);
				_quadVertices[i].Position = new Vector2(x, y);
			}

			_graphicsDevice.UpdateBuffer(_vertexBuffer, 0, _quadVertices);
			_commandList.ClearColorTarget(0, RgbaFloat.White);
			// Set all relevant state to draw our quad.
			_commandList.SetVertexBuffer(0, _vertexBuffer);
			_commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
			_commandList.SetPipeline(_pipeline);
			// Issue a Draw command for a single instance with 4 indices.
			_commandList.DrawIndexed(
				indexCount: 4,
				instanceCount: 1,
				indexStart: 0,
				vertexOffset: 0,
				instanceStart: 0);
		}

		public bool Complete { get; private set; }
		public string GetImage()
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			Complete = false;

			_commandList.Begin();
			_commandList.SetFramebuffer(_offscreenFrameBuffer);

			RedrawContent();
			var textureForRender = _offscreenFrameBuffer.ColorTargets[0].Target;
			 _commandList.CopyTexture(
			 	textureForRender, 0, 0, 0, 0, 0,
			 	_stage, 0, 0, 0, 0, 0,
			 	_stage.Width, _stage.Height, 1, 1);
			_commandList.End();

			_graphicsDevice.SubmitCommands(_commandList);
			//_graphicsDevice.WaitForIdle();
			MappedResourceView<Rgba32> map = _graphicsDevice.Map<Rgba32>(_stage, MapMode.Read);

			//Rgba32[] pixelData = new Rgba32[stage.Width * stage.Height];
			Image<Rgba32> img = new Image<Rgba32>((int) _stage.Width, (int) _stage.Height);
			for (int y = 0; y < _stage.Height; y++)
			{
				for (int x = 0; x < _stage.Width; x++)
				{
					//int index = (int)(y * stage.Width + x);
					img[x, y] = map[x, y]; // <- I have to convert BRGA to RGBA pixels here
				}
			}

			_graphicsDevice.Unmap(_stage);
			using var stream = new MemoryStream();
			img.SaveAsBmp(stream);
			
			Complete = true;

			FrameTime = sw.ElapsedMilliseconds;
			var convert = Convert.ToBase64String(stream.ToArray());
			ConvertTime = sw.ElapsedMilliseconds;
			renderTime = DateTime.Now;
			return convert;
		}

		private void DisposeResources()
		{
			_pipeline.Dispose();
			foreach (Shader shader in _shaders)
			{
				shader.Dispose();
			}

			_commandList.Dispose();
			_vertexBuffer.Dispose();
			_indexBuffer.Dispose();
			_graphicsDevice.Dispose();
		}

		public void Dispose()
		{
			DisposeResources();
		}
	}

	struct VertexPositionColor
	{
		public const uint SizeInBytes = 24;
		public Vector2 Position;
		public RgbaFloat Color;

		public VertexPositionColor(Vector2 position, RgbaFloat color)
		{
			Position = position;
			Color = color;
		}
	}

	static class helper
	{
		public static TextureDescription GetDescription(this Texture texture)
		{
			return new TextureDescription(
				texture.Width, texture.Height,
				texture.Depth, texture.MipLevels, texture.ArrayLayers,
				texture.Format, texture.Usage, texture.Type, texture.SampleCount
			);
		}
	}
}