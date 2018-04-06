﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    public class VirtualDrawingSurface : Panel
    {
        private Compositor compositor;
        private CanvasDevice win2dDevice;
        private CompositionGraphicsDevice comositionGraphicsDevice;
        private SpriteVisual myDrawingVisual;
        private CompositionVirtualDrawingSurface drawingSurface;
        private CompositionSurfaceBrush surfaceBrush;
        private InteractionTracker tracker;
        private VisualInteractionSource interactionSource;
        private CompositionPropertySet animatingPropset;
        private ExpressionAnimation animateMatrix;
        private ExpressionAnimation moveSurfaceExpressionAnimation;
        private ExpressionAnimation moveSurfaceUpDownExpressionAnimation;
        private ExpressionAnimation scaleSurfaceUpDownExpressionAnimation;

        public VirtualDrawingSurface()
        {
            InitializeComposition();
            ConfigureSpriteVisual();
        }

        public void InitializeComposition()
        {
            compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            win2dDevice = CanvasDevice.GetSharedDevice();
            comositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, win2dDevice);
            myDrawingVisual = compositor.CreateSpriteVisual();
            ElementCompositionPreview.SetElementChildVisual(this, myDrawingVisual);
        }

        public void ConfigureSpriteVisual()
        {
            var size = new SizeInt32
            {
                Height = (int)InfiniteCanvas.LargeCanvasWidthHeight,
                Width = (int)InfiniteCanvas.LargeCanvasWidthHeight
            };

            this.drawingSurface = comositionGraphicsDevice.CreateVirtualDrawingSurface(
                size,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);

            this.surfaceBrush = compositor.CreateSurfaceBrush(drawingSurface);
            this.surfaceBrush.Stretch = CompositionStretch.None;
            this.surfaceBrush.HorizontalAlignmentRatio = 0;
            this.surfaceBrush.VerticalAlignmentRatio = 0;
            this.surfaceBrush.TransformMatrix = Matrix3x2.CreateTranslation(0, 0);

            this.myDrawingVisual.Brush = surfaceBrush;
            this.surfaceBrush.Offset = new Vector2(0, 0);
            myDrawingVisual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        }

        public Color Background { get; set; } = Colors.White;

        readonly List<IDrawable> _visibleList = new List<IDrawable>();

        public void ReDraw(Rect viewPort)
        {
            _visibleList.Clear();
            foreach (var drawable in _drawableList)
            {
                if (drawable.IsVisible(viewPort))
                {
                    _visibleList.Add(drawable);
                }
            }

            Rect toDraw;
            var first = _visibleList.FirstOrDefault();
            if (first != null)
            {
                double top = first.Bounds.Top,
                    bottom = first.Bounds.Bottom,
                    left = first.Bounds.Left,
                    right = first.Bounds.Right;

                for (var index = 1; index < _visibleList.Count; index++)
                {
                    var stroke = _visibleList[index];
                    bottom = Math.Max(stroke.Bounds.Bottom, bottom);
                    right = Math.Max(stroke.Bounds.Right, right);
                    top = Math.Min(stroke.Bounds.Top, top);
                    left = Math.Min(stroke.Bounds.Left, left);
                }

                toDraw = new Rect(Math.Max(left, 0), Math.Max(top, 0), Math.Max(right - left, 0), Math.Max(bottom - top, 0));

                toDraw.Union(viewPort);
            }
            else
            {
                toDraw = viewPort;
            }

            using (CanvasDrawingSession drawingSession = CanvasComposition.CreateDrawingSession(drawingSurface, toDraw))
            {
                drawingSession.Clear(Background);
                foreach (var drawable in _visibleList)
                {
                    drawable.Draw(drawingSession, toDraw);
                }
            }
        }

        private readonly List<IDrawable> _drawableList = new List<IDrawable>();

        internal void AddDrawable(IDrawable inkDrawable)
        {
            _drawableList.Add(inkDrawable);
        }

        public void Erase(Point point, Rect viewPort)
        {
            for (var i = _visibleList.Count - 1; i >= 0; i--)
            {
                var drawable = _drawableList[i];
                if (drawable is InkDrawable inkDrawable && drawable.Bounds.Contains(point))
                {
                    foreach (var stroke in inkDrawable.Strokes)
                    {
                        if (stroke.BoundingRect.Contains(point))
                        {
                            _drawableList.RemoveAt(i);
                            ReDraw(viewPort);
                            return;
                        }
                    }
                }
            }
        }

        internal TextDrawable GetEditableTextDrawable(Point point, Rect viewPort)
        {
            for (var i = _visibleList.Count - 1; i >= 0; i--)
            {
                var drawable = _visibleList[i];
                Debug.WriteLine($"{drawable.Bounds.Contains(point)}, {drawable.Bounds}, {point}");
                if (drawable is TextDrawable textDrawable && drawable.Bounds.Contains(point))
                {
                    return textDrawable;
                }
            }

            return null;
        }

        public void ClearAll(Rect viewPort)
        {
            _visibleList.Clear();
            _drawableList.Clear();
            using (CanvasDrawingSession drawingSession = CanvasComposition.CreateDrawingSession(drawingSurface, viewPort))
            {
                drawingSession.Clear(Background);
            }
        }

        internal void RemoveDrawable(IDrawable selectedTextDrawable)
        {
            _drawableList.Remove(selectedTextDrawable);
        }
    }
}
