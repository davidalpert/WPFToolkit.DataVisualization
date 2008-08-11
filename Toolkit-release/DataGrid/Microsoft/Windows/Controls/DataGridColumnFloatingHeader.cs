//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    /// The control which would be used to indicate the drag during column header drag-drop
    /// </summary>
    [TemplatePart(Name = "PART_VisualBrushCanvas", Type = typeof(Canvas))]
    internal class DataGridColumnFloatingHeader : Control
    {
        #region Constructors

        static DataGridColumnFloatingHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridColumnFloatingHeader), new FrameworkPropertyMetadata(typeof(DataGridColumnFloatingHeader)));
            WidthProperty.OverrideMetadata(typeof(DataGridColumnFloatingHeader), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnWidthChanged),
                                                                                                               new CoerceValueCallback(OnCoerceWidth)));
            HeightProperty.OverrideMetadata(typeof(DataGridColumnFloatingHeader), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnHeightChanged),
                                                                                                                new CoerceValueCallback(OnCoerceHeight)));
        }

        #endregion

        #region Static Methods

        private static void OnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGridColumnFloatingHeader header = (DataGridColumnFloatingHeader)d;
            double width = (double)e.NewValue;
            if (header._visualBrushCanvas != null && !Double.IsNaN(width))
            {
                VisualBrush brush = header._visualBrushCanvas.Background as VisualBrush;
                if (brush != null)
                {
                    Rect viewBox = brush.Viewbox;
                    brush.Viewbox = new Rect(viewBox.X, viewBox.Y, width - header.GetVisualCanvasMarginX(), viewBox.Height);
                }
            }
        }

        private static object OnCoerceWidth(DependencyObject d, object baseValue)
        {
            Double width = (double)baseValue;
            DataGridColumnFloatingHeader header = (DataGridColumnFloatingHeader)d;
            if (header._referenceHeader != null && Double.IsNaN(width))
            {
                return header._referenceHeader.ActualWidth + header.GetVisualCanvasMarginX();
            }
            return baseValue;
        }

        private static void OnHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGridColumnFloatingHeader header = (DataGridColumnFloatingHeader)d;
            double height = (double)e.NewValue;
            if (header._visualBrushCanvas != null && !Double.IsNaN(height))
            {
                VisualBrush brush = header._visualBrushCanvas.Background as VisualBrush;
                if (brush != null)
                {
                    Rect viewBox = brush.Viewbox;
                    brush.Viewbox = new Rect(viewBox.X, viewBox.Y, viewBox.Width, height - header.GetVisualCanvasMarginY());
                }
            }
        }

        private static object OnCoerceHeight(DependencyObject d, object baseValue)
        {
            Double height = (double)baseValue;
            DataGridColumnFloatingHeader header = (DataGridColumnFloatingHeader)d;
            if (header._referenceHeader != null && Double.IsNaN(height))
            {
                return header._referenceHeader.ActualHeight + header.GetVisualCanvasMarginY();
            }
            return baseValue;
        }

        #endregion

        #region Methods and Properties

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _visualBrushCanvas = GetTemplateChild(VisualBrushCanvasTemplateName) as Canvas;
            UpdateVisualBrush();
        }

        internal DataGridColumnHeader ReferenceHeader
        {
            get
            {
                return _referenceHeader;
            }
            set
            {
                _referenceHeader = value;
            }
        }

        private void UpdateVisualBrush()
        {
            if (_referenceHeader != null && _visualBrushCanvas != null)
            {
                VisualBrush visualBrush = new VisualBrush(_referenceHeader);

                visualBrush.ViewboxUnits = BrushMappingMode.Absolute;

                double width = Width;
                if (Double.IsNaN(width))
                {
                    width = _referenceHeader.ActualWidth;
                }
                else
                {
                    width = width - GetVisualCanvasMarginX();
                }

                double height = Height;
                if (Double.IsNaN(height))
                {
                    height = _referenceHeader.ActualHeight;
                }
                else
                {
                    height = height - GetVisualCanvasMarginY();
                }

                Vector offset = VisualTreeHelper.GetOffset(_referenceHeader);
                visualBrush.Viewbox = new Rect(offset.X, offset.Y, width, height);

                _visualBrushCanvas.Background = visualBrush;
            }
        }

        internal void ClearHeader()
        {
            _referenceHeader = null;
            if (_visualBrushCanvas != null)
            {
                _visualBrushCanvas.Background = null;
            }
        }

        private double GetVisualCanvasMarginX()
        {
            double delta = 0;
            if (_visualBrushCanvas != null)
            {
                Thickness margin = _visualBrushCanvas.Margin;
                delta += margin.Left;
                delta += margin.Right;
            }
            return delta;
        }

        private double GetVisualCanvasMarginY()
        {
            double delta = 0;
            if (_visualBrushCanvas != null)
            {
                Thickness margin = _visualBrushCanvas.Margin;
                delta += margin.Top;
                delta += margin.Bottom;
            }
            return delta;
        }

        #endregion

        #region Data

        DataGridColumnHeader _referenceHeader = null;
        private const string VisualBrushCanvasTemplateName = "PART_VisualBrushCanvas";
        private Canvas _visualBrushCanvas = null;

        #endregion
    }
}
