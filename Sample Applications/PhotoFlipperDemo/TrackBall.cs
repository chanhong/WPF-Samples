// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace PhotoFlipperDemo
{
    public class Trackball
    {
        private Vector3D _center;
        private bool _centered; // Have we already determined the rotation center?
        // The state of the trackball
        private bool _enabled;
        private Point _point; // Initial point of drag
        private bool _rotating;
        private Quaternion _rotation;
        private Quaternion _rotationDelta; // Change to rotation because of this drag
        private double _scale;
        private double _scaleDelta; // Change to scale because of this drag
        // The state of the current drag
        private bool _scaling; // Are we scaling?  NOTE otherwise we're rotating
        private List<Viewport3D> _servants;
        private Vector3D _translate;
        private Vector3D _translateDelta;

        public Trackball()
        {
            Reset();
        }

        public List<Viewport3D> Servants
        {
            get { return _servants ?? (_servants = new List<Viewport3D>()); }
            set { _servants = value; }
        }

        public bool Enabled
        {
            get { return _enabled && (_servants != null) && (_servants.Count > 0); }
            set { _enabled = value; }
        }

        public void Attach(FrameworkElement element)
        {
            element.MouseMove += MouseMoveHandler;
            element.MouseRightButtonDown += MouseDownHandler;
            element.MouseRightButtonUp += MouseUpHandler;
            element.MouseWheel += OnMouseWheel;
        }

        public void Detach(FrameworkElement element)
        {
            element.MouseMove -= MouseMoveHandler;
            element.MouseRightButtonDown -= MouseDownHandler;
            element.MouseRightButtonUp -= MouseUpHandler;
            element.MouseWheel -= OnMouseWheel;
        }

        // Updates the matrices of the servants using the rotation quaternion.
        private void UpdateServants(Quaternion q, double s, Vector3D t)
        {
            //RotateTransform3D rotation = new RotateTransform3D(q,_center);
            //ScaleTransform3D scale = new ScaleTransform3D(new Vector3D(s,s,s));
            //Transform3DCollection rotateAndScale = new Transform3DCollection(rotation,scale);

            if (_servants != null)
            {
                foreach (var i in _servants)
                {
                    var mv = i.Children[0] as ModelVisual3D;
                    var t3Dg = mv.Transform as Transform3DGroup;

                    var groupScaleTransform = t3Dg.Children[0] as ScaleTransform3D;
                    var groupRotateTransform = t3Dg.Children[1] as RotateTransform3D;
                    var groupTranslateTransform = t3Dg.Children[2] as TranslateTransform3D;

                    groupScaleTransform.ScaleX = s;
                    groupScaleTransform.ScaleY = s;
                    groupScaleTransform.ScaleZ = s;
                    groupRotateTransform.Rotation = new AxisAngleRotation3D(q.Axis, q.Angle);
                    groupTranslateTransform.OffsetX = t.X;
                    groupTranslateTransform.OffsetY = t.Y;
                    groupTranslateTransform.OffsetZ = t.Z;

                    // Note that we don't copy constantly here, we copy the first time someone tries to
                    // trackball a frozen Models, but we replace it with a ChangeableReference
                    // and so subsequent interactions go through without a copy.
                    /*
                    if (i.Models.Transform.IsChangeable)
                    {
                        Model3DGroup mutableCopy = i.Models.Copy();
                        mutableCopy.StatusOfNextUse = UseStatus.ChangeableReference;
                        i.Models = mutableCopy;
                    }
                    i.Models.Transform = rotateAndScale;
                    */
                }
            }
        }

        private void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (!Enabled) return;
            e.Handled = true;

            var el = (UIElement)sender;

            if (el.IsMouseCaptured)
            {
                var delta = _point - e.MouseDevice.GetPosition(el);

                // Adjust the mouse delta based on the current operation
                delta /= 2;

                // Initialize the translation and rotation variables
                var t = new Vector3D();
                var q = _rotation;

                if (_rotating)
                {
                    // We redefine the 2D mouse delta as a 3D mouse delta
                    // where "into the screen" is Z
                    var mouse = new Vector3D(delta.X, -delta.Y, 0);
                    var axis = Vector3D.CrossProduct(mouse, new Vector3D(0, 0, 1));
                    var len = axis.Length;

                    // Calculate the rotation delta
                    if (len < 0.00001 || _scaling)
                        _rotationDelta = new Quaternion(new Vector3D(0, 0, 1), 0);
                    else
                        _rotationDelta = new Quaternion(axis, len);

                    // Update the rotation quaternion
                    q = _rotationDelta * _rotation;
                }
                else
                {
                    // Adjust the translation delta
                    delta /= 20;
                    _translateDelta.X = delta.X*-1;
                    _translateDelta.Y = delta.Y;
                }

                // Update the translation variable
                t = _translate + _translateDelta;

                // Update the servants with the new rotation, scale, and translation
                UpdateServants(q, _scale * _scaleDelta, t);
            }
        }

        private void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (!Enabled) return;
            e.Handled = true;


            if (Keyboard.IsKeyDown(Key.F1))
            {
                Reset();
                return;
            }

            var el = (UIElement) sender;
            _point = e.MouseDevice.GetPosition(el);
            // Initialize the center of rotation to the lookatpoint
            if (!_centered)
            {
                var camera = (ProjectionCamera) _servants[0].Camera;
                _center = camera.LookDirection;
                _centered = true;
            }

            _scaling = (e.MiddleButton == MouseButtonState.Pressed);

            _rotating = Keyboard.IsKeyDown(Key.Space) == false;

            el.CaptureMouse();
        }

        private void MouseUpHandler(object sender, MouseButtonEventArgs e)
        {
            if (!_enabled) return;
            e.Handled = true;

            // Stuff the current initial + delta into initial so when we next move we
            // start at the right place.
            if (_rotating)
                _rotation = _rotationDelta*_rotation;
            else
            {
                _translate += _translateDelta;
                _translateDelta.X = 0;
                _translateDelta.Y = 0;
            }

            //_scale = _scaleDelta*_scale;
            var el = (UIElement) sender;
            el.ReleaseMouseCapture();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            _scaleDelta += e.Delta/(double) 1000;
            var q = _rotation;

            UpdateServants(q, _scale*_scaleDelta, _translate);
        }

        private void MouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
        {
            Reset();
        }

        private void Reset()
        {
            _rotation = new Quaternion(0, 0, 0, 1);
            _scale = 1;
            _translate.X = 0;
            _translate.Y = 0;
            _translate.Z = 0;
            _translateDelta.X = 0;
            _translateDelta.Y = 0;
            _translateDelta.Z = 0;

            // Clear delta too, because if reset is called because of a double click then the mouse
            // up handler will also be called and this way it won't do anything.
            _rotationDelta = Quaternion.Identity;
            _scaleDelta = 1;
            UpdateServants(_rotation, _scale, _translate);
        }
    }
}