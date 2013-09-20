// Original code taken from Dan Fernandez's BUILD talk: Building Apps for the Kinect for Windows SDK
// http://channel9.msdn.com/Events/TechEd/NorthAmerica/2013/DEV-B305
// Original source: http://video.ch9.ms/sessions/teched/na/2013/DEVB305_BuildingAppsWithKinect.zip
// Modifications by Donna Malayeri

using Microsoft.Kinect;
using System;
using System.Linq;
using System.Windows;

using System.Reactive.Linq;
using System.Reactive.Disposables;
using Coding4Fun.Toolkit.Controls.Common;

namespace RxKinect
{
   public partial class MainWindow : Window
   {
      #region Private variables & constructor

      KinectSensor _kinect = null;

      private IDisposable _colorFrameSubscription = Disposable.Empty;
      private IDisposable _skeletonSubscription = Disposable.Empty;

      public MainWindow()
      {
         InitializeComponent();
         DataContext = this;
      }
      #endregion

      private void Window_Loaded(object sender, RoutedEventArgs e)
      {
         _kinect = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected); 

         if (_kinect != null) {

            _coorMapper = new CoordinateMapper(_kinect);

            _colorFrameSubscription = SubscribeToColorFrame(_kinect);

            var joints = GetJointsObservable(_kinect);
            _skeletonSubscription = SubscribeToSkeleton(joints);

            _kinect.Start();
         }
      }

      #region Cleanup
      private void Window_Closed(object sender, EventArgs e)
      {
         if (_kinect != null) {
            _kinect.Stop();
            _colorFrameSubscription.Dispose();
            _skeletonSubscription.Dispose();
         }
      }
      #endregion


      private IObservable<JointCollection> GetJointsObservable(KinectSensor kinect)
      {
         var skeletonFrames = Observable.FromEventPattern<SkeletonFrameReadyEventArgs>(
             addHandler: h => kinect.SkeletonFrameReady += h,
             removeHandler: h => kinect.SkeletonFrameReady -= h
         );

         kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
         kinect.SkeletonStream.Enable(_transformParams);

         var skeletons = skeletonFrames
             .Select(sf => {
                using (var frame = sf.EventArgs.OpenSkeletonFrame()) {
                   if (frame != null) {
                      var sd = new Skeleton[frame.SkeletonArrayLength];
                      frame.CopySkeletonDataTo(sd);
                      return sd;
                   } else return new Skeleton[0];
                }
             });


         var joints = from sd in skeletons
                      let tracked = sd.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked)
                      where tracked != null
                      select tracked.Joints;


         return joints;
      }

      private IDisposable SubscribeToSkeleton(IObservable<JointCollection> joints)
      {
         var subscriptions = new CompositeDisposable();

         var rightHand = joints.Select(joint => joint[JointType.HandRight]);
         var leftHand = joints.Select(joint => joint[JointType.HandLeft]);

         var rightHandSub =
             rightHand.Subscribe(
                joint => {
                   ScalePosition(_rightEllipse, joint); // scale relative to the UI so that user doesn't have to make big movements
                   CheckForNewColor(_rightEllipse);
                });


         var leftHandSub =
             leftHand.Subscribe(joint => MoveToCameraPosition(_leftEllipse, joint));

         subscriptions.Add(rightHandSub);
         subscriptions.Add(leftHandSub);


         // Detect hand motion left/right
         //
         var relPos = (from joint in joints
                       let delta = joint[JointType.HandLeft].Position.X - joint[JointType.ElbowLeft].Position.X
                       where Math.Abs(delta) > 0.05
                       select delta < 0 ? "Left" : "Right")
                      .DistinctUntilChanged();

         var relPosSub = relPos.Subscribe(SetInfoText);
         subscriptions.Add(relPosSub);

         // Detect hand wave (at least 3 moves in 3 seconds)
         //
         var wave = from moves in relPos.Buffer(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(500))
                    where moves.Count >= 3
                    select true;

         var waveSub = 
            wave
            .ObserveOnDispatcher()
            .Subscribe(_ => DoSomething());

         subscriptions.Add(waveSub);

         return subscriptions;
      }

      #region Set up video image from Kinect
      private IDisposable SubscribeToColorFrame(KinectSensor kinect)
      {
         var colorFrames =
            Observable.FromEventPattern<ColorImageFrameReadyEventArgs>(
                  addHandler: h => kinect.ColorFrameReady += h,
                  removeHandler: h => kinect.ColorFrameReady -= h)
            .Select(e => e.EventArgs);

         kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

         return colorFrames.Subscribe(CopyColorFrame);
      }
      #endregion


      private void DoSomething()
      {
         _infoBox.Text += " WAVE!";
         HueLightingWrapper.SetHue(_currentColor);
      }
   }
}
