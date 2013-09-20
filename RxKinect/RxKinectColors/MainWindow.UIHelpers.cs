// Original code taken from Dan Fernandez's BUILD talk: Building Apps for the Kinect for Windows SDK
// http://channel9.msdn.com/Events/TechEd/NorthAmerica/2013/DEV-B305
// Original source: http://video.ch9.ms/sessions/teched/na/2013/DEVB305_BuildingAppsWithKinect.zip
// Modifications by Donna Malayeri (@lindydonna)

using Microsoft.Kinect;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using Coding4Fun.Toolkit.Controls.Common;
using Coding4Fun.Kinect.Wpf;
using System.Windows.Media.Imaging;

namespace RxKinect
{
   partial class MainWindow
   {
      private WriteableBitmap _colorImageWritableBitmap;
      private byte[] _colorImageData;
      private ColorImageFormat _currentColorImageFormat = ColorImageFormat.Undefined;
      private Color _currentColor;
      private CoordinateMapper _coorMapper;

      public SolidColorBrush SetColor
      {
         get { return (SolidColorBrush) GetValue(SetColorProperty); }
         set { SetValue(SetColorProperty, value); }
      }

      // Using a DependencyProperty as the backing store for SetColor.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty SetColorProperty =
          DependencyProperty.Register("SetColor", typeof(SolidColorBrush), typeof(MainWindow), new PropertyMetadata(Brushes.White));

      TransformSmoothParameters _transformParams = new TransformSmoothParameters {
         Smoothing = 0.2f,
         Correction = 0.0f,
         Prediction = 0.0f,
         JitterRadius = 0.6f,
         MaxDeviationRadius = 0.5f
      };

      private void CopyColorFrame(ColorImageFrameReadyEventArgs e)
      {
         using (var colorFrame = e.OpenColorImageFrame()) {
            if (colorFrame == null) {
               return;
            }

            // Make a copy of the color frame for displaying.
            var haveNewFormat = _currentColorImageFormat != colorFrame.Format;
            if (haveNewFormat) {
               _currentColorImageFormat = colorFrame.Format;
               _colorImageData = new byte[colorFrame.PixelDataLength];
               _colorImageWritableBitmap = new WriteableBitmap(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null);

               _colorImage.Source = _colorImageWritableBitmap;
            }

            colorFrame.CopyPixelDataTo(_colorImageData);
            _colorImageWritableBitmap.WritePixels(
                new Int32Rect(0, 0, colorFrame.Width, colorFrame.Height),
                _colorImageData,
                colorFrame.Width * colorFrame.BytesPerPixel,
                0);
         }
      }
      private void CheckForNewColor(Ellipse targetEllipse)
      {
         foreach (var item in new[] { _rectPink, _rectOrange, _rectRed, _rectGreen }) {
            if (IsItemMidpointInContainer(item, targetEllipse))
               ChangeColor(item);
         }
      }

      private void ChangeColor(Rectangle targetObject)
      {
         var newColor = ((SolidColorBrush) targetObject.Fill).Color;

         if (_currentColor == newColor)
            return;

         _currentColor = newColor;
         SetColor = new SolidColorBrush(_currentColor);

         Debug.WriteLine("New color: {0}", SetColor.Color);
      }

      private void MoveToCameraPosition(FrameworkElement element, Joint joint)
      {
         ColorImagePoint point = 
             _coorMapper.MapSkeletonPointToColorPoint(joint.Position, _kinect.ColorStream.Format);

         //Divide by 2 for width and height so point is right in the middle 
         // instead of in top/left corner
         Canvas.SetLeft(element, point.X - element.Width / 2);
         Canvas.SetTop(element, point.Y - element.Height / 2);
      }

      private Joint ScaleJoint(Joint joint)
      {
         return joint.ScaleTo((int) this.ActualWidth, (int) this.ActualHeight, .4f, .4f);
      }

      private void MoveElement(FrameworkElement element, SkeletonPoint pos)
      {
         Canvas.SetLeft(element, pos.X);
         Canvas.SetTop(element, pos.Y);
      }

      private void ScalePosition(FrameworkElement element, Joint joint)
      {
         //convert the value to X/Y
         //convert & scale (.3 = means 1/3 of joint distance)
         //scale to width/height of window - can also set manually 1280x720
         Joint scaledJoint = joint.ScaleTo((int)this.ActualWidth, (int)this.ActualHeight, .4f, .4f);

         Canvas.SetLeft(element, scaledJoint.Position.X);
         Canvas.SetTop(element, scaledJoint.Position.Y);

         //Debug.WriteLine("Scaled(x, y) = {0}, {1}", scaledJoint.Position.X, scaledJoint.Position.Y);
      }

      public static bool IsItemMidpointInContainer(FrameworkElement container, FrameworkElement target)
      {
         var containerTopLeft = container.PointToScreen(new Point());
         var itemTopLeft = target.PointToScreen(new Point());

         double topBoundary = containerTopLeft.Y;
         double bottomBoundary = topBoundary + container.ActualHeight;
         double leftBoundary = containerTopLeft.X;
         double rightBoundary = leftBoundary + container.ActualWidth;

         //use midpoint of item (width or height divided by 2)
         double itemLeft = itemTopLeft.X + (target.ActualWidth / 2);
         double itemTop = itemTopLeft.Y + (target.ActualHeight / 2);

         if (itemTop < topBoundary || bottomBoundary < itemTop) {
            //Midpoint of target is outside of top or bottom
            return false;
         }

         if (itemLeft < leftBoundary || rightBoundary < itemLeft) {
            //Midpoint of target is outside of left or right
            return false;
         }

         return true;
      }

      private void rectangle_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
      {
         ChangeColor((Rectangle) sender);
      }

      private void SetInfoText(object newVal)
      {
         _infoBox.Text = newVal.ToString();
      }
   }
}
