﻿/*
The MIT License (MIT)
Copyright (c) 2018 Helix Toolkit contributors
*/
using System;
using SharpDX;
using SharpDX.Direct3D11;
#if !NETFX_CORE
namespace HelixToolkit.Wpf.SharpDX.Core
#else
namespace HelixToolkit.UWP.Core
#endif
{
    using Shaders;
    public abstract class GeometryRenderCore : RenderCoreBase<ModelStruct>, IGeometryRenderCore
    {
        private RasterizerState rasterState = null;
        public RasterizerState RasterState { get { return rasterState; } }
        public InputLayout VertexLayout { private set; get; }

        public IElementsBufferModel InstanceBuffer { set; get; }

        public IGeometryBufferModel GeometryBuffer{ set; get; }

        private RasterizerStateDescription rasterDescription = new RasterizerStateDescription()
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
        };
        public RasterizerStateDescription RasterDescription
        {
            set
            {
                rasterDescription = value;
                CreateRasterState(value, false);
            }
            get
            {
                return RasterDescription;
            }
        }

        private string defaultPassName = DefaultPassNames.Default;
        /// <summary>
        /// Name of the default pass inside a technique.
        /// <para>Default: <see cref="DefaultPassNames.Default"/></para>
        /// </summary>
        public string DefaultShaderPassName
        {
            set
            {
                if (defaultPassName == value)
                { return; }
                defaultPassName = value;
                if (IsAttached)
                {
                    DefaultShaderPass = EffectTechnique[value];
                }
            }
            get
            {
                return defaultPassName;
            }
        }

        private string defaultShadowPassName = DefaultPassNames.ShadowPass;
        public string DefaultShadowPassName
        {
            set
            {
                if (defaultShadowPassName == value)
                {
                    return;
                }
                defaultShadowPassName = value;
                if (IsAttached)
                {
                    ShadowPass = EffectTechnique[value];
                }
            }
            get
            {
                return defaultShadowPassName;
            }
        }

        protected IShaderPass DefaultShaderPass { private set; get; }
        protected IShaderPass ShadowPass { private set; get; }

        public bool IsThrowingShadow { set; get; } = false;

        protected virtual bool CreateRasterState(RasterizerStateDescription description, bool force)
        {
            rasterDescription = description;
            if (!IsAttached && !force)
            { return false; }
            RemoveAndDispose(ref rasterState);
            rasterState = Collect(new RasterizerState(Device, description));
            return true;
        }

        protected override bool OnAttach(IRenderTechnique technique)
        {
            if(base.OnAttach(technique))
            {
                DefaultShaderPass = technique[DefaultShaderPassName];
                ShadowPass = technique[DefaultShadowPassName];
                this.VertexLayout = technique.Layout;
                CreateRasterState(rasterDescription, true);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Set all necessary states and buffers
        /// </summary>
        /// <param name="context"></param>
        protected override void OnBindRasterState(DeviceContext context)
        {
            context.Rasterizer.State = rasterState;
        }
        /// <summary>
        /// Attach vertex buffer routine
        /// </summary>
        /// <param name="context"></param>
        protected override void OnAttachBuffers(DeviceContext context)
        {
            base.OnAttachBuffers(context);
            GeometryBuffer.AttachBuffers(context, this.VertexLayout, 0);
            InstanceBuffer?.AttachBuffer(context, 1);           
        }

        protected override bool CanRender(IRenderContext context)
        {
            return base.CanRender(context) && GeometryBuffer != null;
        }

        protected override void OnUpdatePerModelStruct(ref ModelStruct model, IRenderContext context)
        {
            model.World = ModelMatrix * context.WorldMatrix;
            model.HasInstances = InstanceBuffer == null ? 0 : InstanceBuffer.HasElements ? 1 : 0;
        }

        protected override ConstantBufferDescription GetModelConstantBufferDescription()
        {
            return new ConstantBufferDescription(DefaultBufferNames.ModelCB, ModelStruct.SizeInBytes);
        }

        /// <summary>
        /// Draw call
        /// </summary>
        /// <param name="context"></param>
        /// <param name="instanceModel"></param>
        protected virtual void OnDraw(DeviceContext context, IElementsBufferModel instanceModel)
        {
            if (GeometryBuffer.IndexBuffer != null)
            {
                if (instanceModel == null || !instanceModel.HasElements)
                {
                    context.DrawIndexed(GeometryBuffer.IndexBuffer.Count, GeometryBuffer.IndexBuffer.Offset, 0);
                }
                else
                {
                    context.DrawIndexedInstanced(GeometryBuffer.IndexBuffer.Count, instanceModel.Buffer.Count, GeometryBuffer.IndexBuffer.Offset, 0, instanceModel.Buffer.Offset);
                }
            }
            else if (GeometryBuffer.VertexBuffer != null)
            {
                if (instanceModel == null || !instanceModel.HasElements)
                {
                    context.Draw(GeometryBuffer.VertexBuffer.Count, GeometryBuffer.VertexBuffer.Offset);
                }
                else
                {
                    context.DrawInstanced(GeometryBuffer.VertexBuffer.Count, instanceModel.Buffer.Count,
                        GeometryBuffer.VertexBuffer.Offset, instanceModel.Buffer.Offset);
                }
            }
        }
    }
}
