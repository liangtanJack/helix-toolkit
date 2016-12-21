﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GeometryModel3D.cs" company="Helix Toolkit">
//   Copyright (c) 2014 Helix Toolkit contributors
// </copyright>
// <summary>
//   Provides a base class for a scene model which contains geometry
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace HelixToolkit.Wpf.SharpDX
{
    using System.Linq;
    using System.Windows;
    using System.Collections.Generic;

    using global::SharpDX;
    using global::SharpDX.Direct3D11;

    using Point = System.Windows.Point;
    using System.ComponentModel;

    /// <summary>
    /// Provides a base class for a scene model which contains geometry
    /// </summary>
    public abstract class GeometryModel3D : Model3D, IHitable, IBoundable, IVisible, IThrowingShadow, ISelectable, IMouse3D
    {
        protected RasterizerState rasterState;

        /// <summary>
        /// Override in derived classes to specify the
        /// size, in bytes, of the vertices used for rendering.
        /// </summary>
        public virtual int VertexSizeInBytes
        {
            get { return DefaultVertex.SizeInBytes; }
        }

        public Geometry3D Geometry
        {
            get { return (Geometry3D)this.GetValue(GeometryProperty); }
            set
            {
                this.SetValue(GeometryProperty, value);
            }
        }

        public static readonly DependencyProperty ReuseVertexArrayBufferProperty = DependencyProperty.Register("ReuseVertexArrayBuffer", typeof(bool), typeof(GeometryModel3D),
            new PropertyMetadata(false));

        /// <summary>
        /// Reuse previous vertext array buffer during CreateBuffer. Reduce excessive memory allocation during rapid geometry model changes. 
        /// Example: Repeatly updates textures, or geometries with close number of vertices.
        /// </summary>
        public bool ReuseVertexArrayBuffer
        {
            set
            {
                SetValue(ReuseVertexArrayBufferProperty, value);
            }
            get
            {
                return (bool)GetValue(ReuseVertexArrayBufferProperty);
            }
        }

        public static readonly DependencyProperty GeometryProperty =
            DependencyProperty.Register("Geometry", typeof(Geometry3D), typeof(GeometryModel3D), new UIPropertyMetadata(GeometryChanged));

        protected static void GeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var model = d as GeometryModel3D;
            if (e.OldValue != null)
            {
                (e.OldValue as INotifyPropertyChanged).PropertyChanged -= model.OnGeometryPropertyChangedPrivate;
            }
            if (e.NewValue != null)
            {
                (e.NewValue as INotifyPropertyChanged).PropertyChanged -= model.OnGeometryPropertyChangedPrivate;
                (e.NewValue as INotifyPropertyChanged).PropertyChanged += model.OnGeometryPropertyChangedPrivate;
            }
            model.OnGeometryChanged(e);
        }

        protected virtual void OnGeometryChanged(DependencyPropertyChangedEventArgs e)
        {
            if (this.Geometry == null)
            {
                this.Bounds = new BoundingBox();
                return;
            }
            //var m = this.Transform.ToMatrix();
            //var b = BoundingBox.FromPoints(this.Geometry.Positions.Select(x => Vector3.TransformCoordinate(x, m)).ToArray());
            var b = BoundingBox.FromPoints(this.Geometry.Positions.Array);

            //var b = BoundingBox.FromPoints(this.Geometry.Positions);
            //b.Minimum = Vector3.TransformCoordinate(b.Minimum, m);
            //b.Maximum = Vector3.TransformCoordinate(b.Maximum, m);
            this.Bounds = b;
            //this.BoundsDiameter = (b.Maximum - b.Minimum).Length();
            if (renderHost !=null)
            {
                var host = this.renderHost;
                this.Detach();
                this.Attach(host);
            }
        }

        private void OnGeometryPropertyChangedPrivate(object sender, PropertyChangedEventArgs e)
        {
            if (this.IsAttached)
            {
                OnGeometryPropertyChanged(sender, e);
            }
        }

        protected virtual void OnGeometryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {

        }

        protected override void OnTransformChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnTransformChanged(e);
            if (this.Geometry != null)
            {
                //var b = BoundingBox.FromPoints(this.Geometry.Positions.Select(x => Vector3.TransformCoordinate(x, this.modelMatrix)).ToArray());

                //Bounds do not change when transformation changes, only the position of it changes.
                //var b = BoundingBox.FromPoints(this.Geometry.Positions.Array);
                //this.Bounds = b;
                //this.BoundsDiameter = (b.Maximum - b.Minimum).Length();
            }
        }

        public BoundingBox Bounds
        {
            get { return (BoundingBox)this.GetValue(BoundsProperty); }
            protected set { this.SetValue(BoundsPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey BoundsPropertyKey =
            DependencyProperty.RegisterReadOnly("Bounds", typeof(BoundingBox), typeof(GeometryModel3D), new UIPropertyMetadata(new BoundingBox()));

        public static readonly DependencyProperty BoundsProperty = BoundsPropertyKey.DependencyProperty;


        public int DepthBias
        {
            get { return (int)this.GetValue(DepthBiasProperty); }
            set { this.SetValue(DepthBiasProperty, value); }
        }

        public static readonly DependencyProperty DepthBiasProperty =
            DependencyProperty.Register("DepthBias", typeof(int), typeof(GeometryModel3D), new UIPropertyMetadata(0, RasterStateChanged));

        protected static void RasterStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GeometryModel3D)d).OnRasterStateChanged();
        }

        /// <summary>
        /// Make sure to check if <see cref="Element3D.IsAttached"/> == true
        /// </summary>
        protected virtual void OnRasterStateChanged() { }

        public static readonly RoutedEvent MouseDown3DEvent =
            EventManager.RegisterRoutedEvent("MouseDown3D", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Model3D));

        public static readonly RoutedEvent MouseUp3DEvent =
            EventManager.RegisterRoutedEvent("MouseUp3D", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Model3D));

        public static readonly RoutedEvent MouseMove3DEvent =
            EventManager.RegisterRoutedEvent("MouseMove3D", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Model3D));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(DraggableGeometryModel3D), new UIPropertyMetadata(false));

        public static readonly DependencyProperty IsMultisampleEnabledProperty = 
            DependencyProperty.Register("IsMultisampleEnabled", typeof(bool), typeof(GeometryModel3D), new UIPropertyMetadata(true, RasterStateChanged));

        public static readonly DependencyProperty FillModeProperty = DependencyProperty.Register("FillMode", typeof(FillMode), typeof(GeometryModel3D), 
            new PropertyMetadata(FillMode.Solid, RasterStateChanged));

        /// <summary>
        /// Provide CLR accessors for the event 
        /// </summary>
        public event RoutedEventHandler MouseDown3D
        {
            add { AddHandler(MouseDown3DEvent, value); }
            remove { RemoveHandler(MouseDown3DEvent, value); }
        }

        /// <summary>
        /// Provide CLR accessors for the event 
        /// </summary>
        public event RoutedEventHandler MouseUp3D
        {
            add { AddHandler(MouseUp3DEvent, value); }
            remove { RemoveHandler(MouseUp3DEvent, value); }
        }

        /// <summary>
        /// Provide CLR accessors for the event 
        /// </summary>
        public event RoutedEventHandler MouseMove3D
        {
            add { AddHandler(MouseMove3DEvent, value); }
            remove { RemoveHandler(MouseMove3DEvent, value); }
        }

        ///// <summary>
        ///// This method raises the MouseDown3D event 
        ///// </summary>        
        //internal void RaiseMouseDown3DEvent(MouseDown3DEventArgs args)
        //{
        //    this.RaiseEvent(args);
        //}

        ///// <summary>
        ///// This method raises the MouseUp3D event 
        ///// </summary>        
        //internal void RaiseMouseUp3DEvent(MouseUp3DEventArgs args)
        //{
        //    this.RaiseEvent(args);
        //}

        ///// <summary>
        ///// This method raises the MouseMove3D event 
        ///// </summary>        
        //internal void RaiseMouseMove3DEvent(MouseMove3DEventArgs args)
        //{
        //    this.RaiseEvent(args);
        //}
        public GeometryModel3D()
        {
            this.MouseDown3D += OnMouse3DDown;
            this.MouseUp3D += OnMouse3DUp;
            this.MouseMove3D += OnMouse3DMove;
            this.IsThrowingShadow = true;
            //count++;
        }

        /// <summary>
        /// <para>Check geometry validity.</para>
        /// Return false if (this.Geometry == null || this.Geometry.Positions == null || this.Geometry.Positions.Count == 0 || this.Geometry.Indices == null || this.Geometry.Indices.Count == 0)
        /// </summary>
        /// <returns>
        /// </returns>
        protected virtual bool CheckGeometry()
        {
            if (this.Geometry == null || this.Geometry.Positions == null || this.Geometry.Positions.Count == 0
                || this.Geometry.Indices == null || this.Geometry.Indices.Count == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Overriding OnAttach, use <see cref="CheckGeometry"/> to check if it can be attached.
        /// </summary>
        /// <param name="host"></param>
        protected override bool OnAttach(IRenderHost host)
        {
            if (CheckGeometry())
            {
                AttachOnGeometryPropertyChanged();
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            OnRasterStateChanged();
        }

        protected override void OnDetach()
        {
            DetachOnGeometryPropertyChanged();
            Disposer.RemoveAndDispose(ref rasterState);
            base.OnDetach();
        }

        private void AttachOnGeometryPropertyChanged()
        {
            if (Geometry != null)
            {
                Geometry.PropertyChanged -= OnGeometryPropertyChangedPrivate;
                Geometry.PropertyChanged += OnGeometryPropertyChangedPrivate;
            }
        }

        private void DetachOnGeometryPropertyChanged()
        {
            if (Geometry != null)
            {
                Geometry.PropertyChanged -= OnGeometryPropertyChangedPrivate;
            }
        }

        /// <summary>
        /// <para>base.CanRender(context) &amp;&amp; <see cref="CheckGeometry"/> </para>
        /// <para>If RenderContext IsShadowPass=true, return false if <see cref="IsThrowingShadow"/> = false</para>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override bool CanRender(RenderContext context)
        {
            if (base.CanRender(context) && CheckGeometry())
            {
                if (context.IsShadowPass)
                    if (!IsThrowingShadow)
                        return false;
                return true;
            }
            else
            {
                return false;
            }
        }

        ~GeometryModel3D()
        {
            
        }

        //static ulong count = 0;

        public virtual void OnMouse3DDown(object sender, RoutedEventArgs e) { }

        public virtual void OnMouse3DUp(object sender, RoutedEventArgs e) { }

        public virtual void OnMouse3DMove(object sender, RoutedEventArgs e) { }

        /// <summary>
        /// Checks if the ray hits the geometry of the model.
        /// If there a more than one hit, result returns the hit which is nearest to the ray origin.
        /// </summary>
        /// <param name="rayWS">Hitring ray from the camera.</param>
        /// <param name="result">results of the hit.</param>
        /// <returns>True if the ray hits one or more times.</returns>
        public virtual bool HitTest(Ray rayWS, ref List<HitTestResult> hits)
        {
            if (this.Visibility == Visibility.Collapsed)
            {
                return false;
            }
            if (this.IsHitTestVisible == false)
            {
                return false;
            }

            var g = this.Geometry as MeshGeometry3D;
            var isHit = false;
            var result = new HitTestResult();
            result.Distance = double.MaxValue;

            if (g != null)
            {
                var m = this.modelMatrix;

                // put bounds to world space
                var b = BoundingBox.FromPoints(this.Bounds.GetCorners().Select(x => Vector3.TransformCoordinate(x, m)).ToArray());

                //var b = this.Bounds;

                // this all happens now in world space now:
                if (rayWS.Intersects(ref b))
                {
                    int index = 0;
                    foreach (var t in g.Triangles)
                    {
                        float d;
                        var p0 = Vector3.TransformCoordinate(t.P0, m);
                        var p1 = Vector3.TransformCoordinate(t.P1, m);
                        var p2 = Vector3.TransformCoordinate(t.P2, m);
                        if (Collision.RayIntersectsTriangle(ref rayWS, ref p0, ref p1, ref p2, out d))
                        {
                            if (d > 0 && d < result.Distance) // If d is NaN, the condition is false.
                            {
                                result.IsValid = true;
                                result.ModelHit = this;
                                // transform hit-info to world space now:
                                result.PointHit = (rayWS.Position + (rayWS.Direction * d)).ToPoint3D();
                                result.Distance = d;

                                var n = Vector3.Cross(p1 - p0, p2 - p0);
                                n.Normalize();
                                // transform hit-info to world space now:
                                result.NormalAtHit = n.ToVector3D();// Vector3.TransformNormal(n, m).ToVector3D();
                                result.TriangleIndices = new System.Tuple<int, int, int>(g.Indices[index], g.Indices[index + 1], g.Indices[index + 2]);
                                isHit = true;
                            }
                        }
                        index += 3;
                    }
                }
            }
            if (isHit)
            {
                hits.Add(result);
            }
            return isHit;
        }

        /*
        public virtual bool HitTestMS(Ray rayWS, ref List<HitTestResult> hits)
        {
            if (this.Visibility == Visibility.Collapsed)
            {
                return false;
            }

            var result = new HitTestResult();
            result.Distance = double.MaxValue;
            var g = this.Geometry as MeshGeometry3D;
            var h = false;

            if (g != null)
            {
                var m = this.modelMatrix;
                var mi = Matrix.Invert(m);

                // put the ray to model space
                var rayMS = new Ray(Vector3.TransformNormal(rayWS.Direction, mi), Vector3.TransformCoordinate(rayWS.Position, mi));

                // bounds are in model space
                var b = this.Bounds;

                // this all happens now in model space now:
                if (rayMS.Intersects(ref b))
                {
                    foreach (var t in g.Triangles)
                    {
                        float d;
                        var p0 = t.P0;
                        var p1 = t.P1;
                        var p2 = t.P2;
                        if (Collision.RayIntersectsTriangle(ref rayMS, ref p0, ref p1, ref p2, out d))
                        {
                            if (d < result.Distance)
                            {
                                result.IsValid = true;
                                result.ModelHit = this;
                                // transform hit-info to world space now:
                                result.PointHit = Vector3.TransformCoordinate((rayMS.Position + (rayMS.Direction * d)), m).ToPoint3D();
                                result.Distance = d;

                                var n = Vector3.Cross(p1 - p0, p2 - p0);
                                n.Normalize();
                                // transform hit-info to world space now:
                                result.NormalAtHit = Vector3.TransformNormal(n, m).ToVector3D();
                            }
                            h = true;
                        }
                    }
                }
            }

            if (h)
            {
                result.IsValid = h;
                hits.Add(result);
            }
            return h;
        }
        */

        public bool IsThrowingShadow
        {
            get;
            set;
        }

        public bool IsSelected
        {
            get { return (bool)this.GetValue(IsSelectedProperty); }
            set { this.SetValue(IsSelectedProperty, value); }
        }

        /// <summary>
        /// Only works under FillMode = Wireframe. MSAA is determined by viewport MSAA settings for FillMode = Solid
        /// </summary>
        public bool IsMultisampleEnabled
        {
            set { SetValue(IsMultisampleEnabledProperty, value); }
            get { return (bool)GetValue(IsMultisampleEnabledProperty); }
        }

        public FillMode FillMode
        {
            set
            {
                SetValue(FillModeProperty, value);
            }
            get
            {
                return (FillMode)GetValue(FillModeProperty);
            }
        }
    }




    public abstract class Mouse3DEventArgs : RoutedEventArgs
    {
        public HitTestResult HitTestResult { get; private set; }
        public Viewport3DX Viewport { get; private set; }
        public Point Position { get; private set; }

        public Mouse3DEventArgs(RoutedEvent routedEvent, object source, HitTestResult hitTestResult, Point position, Viewport3DX viewport = null)
            : base(routedEvent, source)
        {
            this.HitTestResult = hitTestResult;
            this.Position = position;
            this.Viewport = viewport;
        }
    }

    public class MouseDown3DEventArgs : Mouse3DEventArgs
    {
        public MouseDown3DEventArgs(object source, HitTestResult hitTestResult, Point position, Viewport3DX viewport = null)
            : base(GeometryModel3D.MouseDown3DEvent, source, hitTestResult, position, viewport)
        { }
    }

    public class MouseUp3DEventArgs : Mouse3DEventArgs
    {
        public MouseUp3DEventArgs(object source, HitTestResult hitTestResult, Point position, Viewport3DX viewport = null)
            : base(GeometryModel3D.MouseUp3DEvent, source, hitTestResult, position, viewport)
        { }
    }

    public class MouseMove3DEventArgs : Mouse3DEventArgs
    {
        public MouseMove3DEventArgs(object source, HitTestResult hitTestResult, Point position, Viewport3DX viewport = null)
            : base(GeometryModel3D.MouseMove3DEvent, source, hitTestResult, position, viewport)
        { }
    }
}