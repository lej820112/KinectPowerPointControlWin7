using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.Speech.Recognition;
using System.Threading;
using System.IO;
using System.Speech.AudioFormat;
using System.Diagnostics;
using System.Windows.Threading;

namespace KinectPowerPointControl
{
    /// <summary>
    /// kinect.xaml 的互動邏輯
    /// </summary>
    public partial class kinect : Window
    {
       
        MainWindow mywindow = null;
        KinectSensor sensor;
        SpeechRecognitionEngine speechRecognizer;
        private SolidColorBrush[] _skeletonBrush;  //畫刷顏色陣列
        DispatcherTimer readyTimer;

        byte[] colorBytes;
        Skeleton[] skeletons;

        bool isCirclesVisible = true;
        bool isRightHandOverHead = false;
        bool isLeftHandOverHead = false;
        bool isForwardGestureActive = false;
        bool isBackGestureActive = false;
        bool CheckGesture_ready = false;
        SolidColorBrush activeBrush = new SolidColorBrush(Colors.Green);
        SolidColorBrush inactiveBrush = new SolidColorBrush(Colors.Red);
        SolidColorBrush Black = new SolidColorBrush(Colors.Black);
        public kinect(MainWindow temp)
        {

            mywindow = temp;
            InitializeComponent();
            //定義陣列內容
            _skeletonBrush = new[] { Brushes.Red, Brushes.MintCream, Brushes.Navy, Brushes.Orange, Brushes.Purple, Brushes.Blue };
            //Runtime initialization is handled when the window is opened. When the window
            //is closed, the runtime MUST be unitialized.
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            //Handle the content obtained from the video camera, once received.

            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);
        }
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            sensor = KinectSensor.KinectSensors.FirstOrDefault();

            if (sensor == null)
            {
                MessageBox.Show("This application requires a Kinect sensor.");
                this.Close();
            }

            sensor.Start();

            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            sensor.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(sensor_ColorFrameReady);

            sensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);

            sensor.SkeletonStream.Enable();
            sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(sensor_SkeletonFrameReady);

            //sensor.ElevationAngle = 10;

            Application.Current.Exit += new ExitEventHandler(Current_Exit);

            //InitializeSpeechRecognition();
        }
        void Current_Exit(object sender, ExitEventArgs e)
        {
            /*if (speechRecognizer != null)
            {
                speechRecognizer.RecognizeAsyncCancel();
                speechRecognizer.RecognizeAsyncStop();
            }*/
            if (sensor != null)
            {
                sensor.AudioSource.Stop();
                sensor.Stop();
                sensor.Dispose();
                sensor = null;
            }
        }
        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C)
            {
                ToggleCircles();
            }
        }
        void sensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var image = e.OpenColorImageFrame())
            {
                if (image == null)
                    return;

                if (colorBytes == null ||
                    colorBytes.Length != image.PixelDataLength)
                {
                    colorBytes = new byte[image.PixelDataLength];
                }

                image.CopyPixelDataTo(colorBytes);

                //You could use PixelFormats.Bgr32 below to ignore the alpha,
                //or if you need to set the alpha you would loop through the bytes 
                //as in this loop below
                int length = colorBytes.Length;
                for (int i = 0; i < length; i += 4)
                {
                    colorBytes[i + 3] = 255;
                }

                BitmapSource source = BitmapSource.Create(image.Width,
                    image.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    colorBytes,
                    image.Width * image.BytesPerPixel);
                videoImage.Source = source;
            }
        }

        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                    return;

                if (skeletons == null ||
                    skeletons.Length != skeletonFrame.SkeletonArrayLength)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                skeletonFrame.CopySkeletonDataTo(skeletons);
            }

            Skeleton closestSkeleton = skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked)
                                                .OrderBy(s => s.Position.Z * Math.Abs(s.Position.X))
                                                .FirstOrDefault();

            if (closestSkeleton == null)
                return;

            var head = closestSkeleton.Joints[JointType.Head];
            var rightHand = closestSkeleton.Joints[JointType.HandRight];
            var leftHand = closestSkeleton.Joints[JointType.HandLeft];
            var elbowright = closestSkeleton.Joints[JointType.ElbowRight];
            var elbowleft = closestSkeleton.Joints[JointType.ElbowLeft];
            var ankleleft = closestSkeleton.Joints[JointType.AnkleLeft];
            var ankleright = closestSkeleton.Joints[JointType.AnkleRight];



            if (head.TrackingState == JointTrackingState.NotTracked ||
                rightHand.TrackingState == JointTrackingState.NotTracked ||
                leftHand.TrackingState == JointTrackingState.NotTracked ||
                ankleleft.TrackingState == JointTrackingState.NotTracked ||
                ankleright.TrackingState == JointTrackingState.NotTracked)
            {
                //Don't have a good read on the joints so we cannot process gestures
                return;
            }

            SetEllipsePosition(ellipseHead, head, false);
            SetEllipsePosition(ellipseLeftHand, leftHand, isBackGestureActive);
            SetEllipsePosition(ellipseRightHand, rightHand, isForwardGestureActive);
            SetEllipsePosition(ellipseAnkleLeft, ankleleft, isBackGestureActive);
            SetEllipsePosition(ellipseAnkleRight, ankleright, isForwardGestureActive);

            if (mywindow.radioButton1.IsChecked == true)
            ProcessForwardBackGesture(head, rightHand, leftHand);
            else if (mywindow.radioButton2.IsChecked == true)
            ProcessForwardBackGesture2(head, ankleright, ankleleft);
        }

        //This method is used to position the ellipses on the canvas
        //according to correct movements of the tracked joints.
        private void SetEllipsePosition(Ellipse ellipse, Joint joint, bool isHighlighted)
        {
            if (isHighlighted)
            {
                ellipse.Width = 60;
                ellipse.Height = 60;
                ellipse.Fill = activeBrush;
            }
            else
            {
                ellipse.Width = 20;
                ellipse.Height = 20;
                ellipse.Fill = inactiveBrush;
            }

            CoordinateMapper mapper = sensor.CoordinateMapper;

            var point = mapper.MapSkeletonPointToColorPoint(joint.Position, sensor.ColorStream.Format);

            Canvas.SetLeft(ellipse, point.X - ellipse.ActualWidth / 2);
            Canvas.SetTop(ellipse, point.Y - ellipse.ActualHeight / 2);
        }

        private void ProcessForwardBackGesture(Joint head, Joint rightHand, Joint leftHand)
        {
            if (rightHand.Position.X > head.Position.X + 0.45)
            {
                if (!isForwardGestureActive)
                {
                    isForwardGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{Right}");
                }
            }
            else
            {
                isForwardGestureActive = false;
            }

            if (leftHand.Position.X < head.Position.X - 0.45)
            {
                if (!isBackGestureActive)
                {
                    isBackGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{Left}");
                }
            }
            else
            {
                isBackGestureActive = false;
            }

            if (rightHand.Position.Y > head.Position.Y)
            {
                CheckGesture();
                if (CheckGesture_ready)
                {
                    isRightHandOverHead = true;
                    System.Windows.Forms.SendKeys.SendWait("{Home}");
                }
            }
            else
            {
                isRightHandOverHead = false;
            }

            if (leftHand.Position.Y > head.Position.Y)
            {
                CheckGesture();
                if (CheckGesture_ready)
                {
                    isLeftHandOverHead = true;
                    System.Windows.Forms.SendKeys.SendWait("%" + "{F4}");
                }
            }
            else
            {
                isLeftHandOverHead = false;
            }


        }
        private void ProcessForwardBackGesture2(Joint head, Joint ankleright, Joint ankleleft)
        {
            if (ankleright.Position.Z > head.Position.Z + 0.45)
            {
                if (!isForwardGestureActive)
                {
                    isForwardGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{Right}");
                }
            }
            else
            {
                isForwardGestureActive = false;
            }

            if (ankleleft.Position.Z < head.Position.Z - 0.45)
            {
                if (!isBackGestureActive)
                {
                    isBackGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{Left}");
                }
            }
            else
            {
                isBackGestureActive = false;
            }

            if (ankleright.Position.Y > head.Position.Y)
            {
                CheckGesture();
                if (CheckGesture_ready)
                {
                    isRightHandOverHead = true;
                    System.Windows.Forms.SendKeys.SendWait("{Home}");
                }
            }
            else
            {
                isRightHandOverHead = false;
            }

            if (ankleleft.Position.Y > head.Position.Y)
            {
                CheckGesture();
                if (CheckGesture_ready)
                {
                    isLeftHandOverHead = true;
                    System.Windows.Forms.SendKeys.SendWait("%" + "{F4}");
                }
            }
            else
            {
                isLeftHandOverHead = false;
            }


        }
        private void CheckGesture()
        {
            if (!isForwardGestureActive && !isBackGestureActive && !isRightHandOverHead && !isLeftHandOverHead)
            {
                CheckGesture_ready = true;
            }
            else
            {
                CheckGesture_ready = false;
            }
        }

        private void stoppost(Joint rightelbow, Joint leftelbow, Joint rightHand, Joint leftHand)
        {
            const double Threshold = 0.15;
            if (rightHand.Position.Y > rightelbow.Position.Y && leftHand.Position.Y > leftelbow.Position.Y)
            {
                if (rightelbow.Position.X > leftelbow.Position.X && rightHand.Position.X < leftHand.Position.X)
                {
                    if (rightHand.Position.X < rightelbow.Position.X && leftHand.Position.X > leftelbow.Position.X)
                    {
                        if (Math.Abs(rightHand.Position.Z - leftHand.Position.Z) < Threshold)
                        {
                            System.Windows.Forms.SendKeys.SendWait("{ESC}");
                        }
                    }
                }
            }
        }

        private void Kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons;  //骨架陣列

            using (SkeletonFrame frameData = e.OpenSkeletonFrame())  //取得傳遞的影格資料
            {
                if (frameData == null)  //如果影格資料不存在,直接離開事件處理函式
                {
                    return;
                }
                //將影格資料複製到骨架陣列
                StickFigure.Children.Clear();
                skeletons = new Skeleton[frameData.SkeletonArrayLength];
                frameData.CopySkeletonDataTo(skeletons);
                int brushesIndex = 0;
                foreach (Skeleton skeleton in skeletons)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Brush brush = _skeletonBrush[brushesIndex];
                        //脊髓
                        DrawLine(skeleton.Joints[JointType.Head], skeleton.Joints[JointType.ShoulderCenter], brush);
                        DrawLine(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.Spine], brush);
                        DrawLine(skeleton.Joints[JointType.Spine], skeleton.Joints[JointType.HipCenter], brush);
                        //左臂
                        DrawLine(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.ShoulderLeft], brush);
                        DrawLine(skeleton.Joints[JointType.ShoulderLeft], skeleton.Joints[JointType.ElbowLeft], brush);
                        DrawLine(skeleton.Joints[JointType.ElbowLeft], skeleton.Joints[JointType.WristLeft], brush);
                        DrawLine(skeleton.Joints[JointType.WristLeft], skeleton.Joints[JointType.HandLeft], brush);
                        //右臂
                        DrawLine(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.ShoulderRight], brush);
                        DrawLine(skeleton.Joints[JointType.ShoulderRight], skeleton.Joints[JointType.ElbowRight], brush);
                        DrawLine(skeleton.Joints[JointType.ElbowRight], skeleton.Joints[JointType.WristRight], brush);
                        DrawLine(skeleton.Joints[JointType.WristRight], skeleton.Joints[JointType.HandRight], brush);
                        //左腳
                        DrawLine(skeleton.Joints[JointType.HipCenter], skeleton.Joints[JointType.HipLeft], brush);
                        DrawLine(skeleton.Joints[JointType.HipLeft], skeleton.Joints[JointType.KneeLeft], brush);
                        DrawLine(skeleton.Joints[JointType.KneeLeft], skeleton.Joints[JointType.AnkleLeft], brush);
                        DrawLine(skeleton.Joints[JointType.AnkleLeft], skeleton.Joints[JointType.FootLeft], brush);
                        //右腳
                        DrawLine(skeleton.Joints[JointType.HipCenter], skeleton.Joints[JointType.HipRight], brush);
                        DrawLine(skeleton.Joints[JointType.HipRight], skeleton.Joints[JointType.KneeRight], brush);
                        DrawLine(skeleton.Joints[JointType.KneeRight], skeleton.Joints[JointType.AnkleRight], brush);
                        DrawLine(skeleton.Joints[JointType.AnkleRight], skeleton.Joints[JointType.FootRight], brush);
                        //下一個畫刷顏色
                        brushesIndex++;
                    }
                }
            }
        }
        //劃線與顯示函式-- 
        private void DrawLine(Joint joint1, Joint joint2, Brush brush)
        {
            Line stickLine = new Line();
            stickLine.Stroke = brush;
            stickLine.StrokeThickness = 5;
            ColorImagePoint point1 = sensor.MapSkeletonPointToColor(joint1.Position, ColorImageFormat.RgbResolution640x480Fps30);
            stickLine.X1 = point1.X;
            stickLine.Y1 = point1.Y;
            ColorImagePoint point2 = sensor.MapSkeletonPointToColor(joint2.Position, ColorImageFormat.RgbResolution640x480Fps30);
            stickLine.X2 = point2.X;
            stickLine.Y2 = point2.Y;

            StickFigure.Children.Add(stickLine);
        }
        void ToggleCircles()
        {
            if (isCirclesVisible)
                HideCircles();
            else
                ShowCircles();
        }

        void HideCircles()
        {
            isCirclesVisible = false;
            ellipseHead.Visibility = System.Windows.Visibility.Collapsed;
            ellipseLeftHand.Visibility = System.Windows.Visibility.Collapsed;
            ellipseRightHand.Visibility = System.Windows.Visibility.Collapsed;
            ellipseAnkleLeft.Visibility = System.Windows.Visibility.Collapsed;
            ellipseAnkleRight.Visibility = System.Windows.Visibility.Collapsed;
        }

        void ShowCircles()
        {
            isCirclesVisible = true;
            ellipseHead.Visibility = System.Windows.Visibility.Visible;
            ellipseLeftHand.Visibility = System.Windows.Visibility.Visible;
            ellipseRightHand.Visibility = System.Windows.Visibility.Visible;
            ellipseAnkleLeft.Visibility = System.Windows.Visibility.Visible;
            ellipseAnkleRight.Visibility = System.Windows.Visibility.Visible;
        }

        private void ShowWindow()
        {
            this.Topmost = true;
            this.WindowState = System.Windows.WindowState.Maximized;
        }

        private void HideWindow()
        {
            this.Topmost = false;
            this.WindowState = System.Windows.WindowState.Minimized;
        }
        void End()
        {
            Close();
        }
        void pageup()
        {
            System.Windows.Forms.SendKeys.SendWait("{Right}");
        }
        void pagedown()
        {
            System.Windows.Forms.SendKeys.SendWait("{Left}");
        }
       
        #region Speech Recognition Methods

        private static RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
            };
            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

        private void InitializeSpeechRecognition()
        {
            RecognizerInfo ri = GetKinectRecognizer();
            if (ri == null)
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                speechRecognizer = new SpeechRecognitionEngine(ri.Id);
            }
            catch
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed and configured.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            var phrases = new Choices();
            phrases.Add("computer show window");
            phrases.Add("computer hide window");
            phrases.Add("computer show circles");
            phrases.Add("computer hide circles");
            phrases.Add("End");



            var gb = new GrammarBuilder();
            //Specify the culture to match the recognizer in case we are running in a different culture.                                 
            gb.Culture = ri.Culture;
            gb.Append(phrases);

            // Create the actual Grammar instance, and then load it into the speech recognizer.
            var g = new Grammar(gb);

            speechRecognizer.LoadGrammar(g);
            speechRecognizer.SpeechRecognized += SreSpeechRecognized;
            speechRecognizer.SpeechHypothesized += SreSpeechHypothesized;
            speechRecognizer.SpeechRecognitionRejected += SreSpeechRecognitionRejected;

            this.readyTimer = new DispatcherTimer();
            this.readyTimer.Tick += this.ReadyTimerTick;
            this.readyTimer.Interval = new TimeSpan(0, 0, 4);
            this.readyTimer.Start();

        }

        private void ReadyTimerTick(object sender, EventArgs e)
        {
            this.StartSpeechRecognition();
            this.readyTimer.Stop();
            this.readyTimer.Tick -= ReadyTimerTick;
            this.readyTimer = null;
        }

        private void StartSpeechRecognition()
        {
            if (sensor == null || speechRecognizer == null)
                return;

            var audioSource = this.sensor.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            var kinectStream = audioSource.Start();

            speechRecognizer.SetInputToAudioStream(
                    kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);

        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Trace.WriteLine("\nSpeech Rejected, confidence: " + e.Result.Confidence);
        }

        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Trace.Write("\rSpeech Hypothesized: \t{0}", e.Result.Text);
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //This first release of the Kinect language pack doesn't have a reliable confidence model, so 
            //we don't use e.Result.Confidence here.
            if (e.Result.Confidence < 0.70)
            {
                Trace.WriteLine("\nSpeech Rejected filtered, confidence: " + e.Result.Confidence);
                return;
            }

            Trace.WriteLine("\nSpeech Recognized, confidence: " + e.Result.Confidence + ": \t{0}", e.Result.Text);

            if (e.Result.Text == "computer show window")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    ShowWindow();
                });
            }
            else if (e.Result.Text == "computer hide window")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    HideWindow();
                });
            }
            else if (e.Result.Text == "computer hide circles")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.HideCircles();
                });
            }
            else if (e.Result.Text == "computer show circles")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.ShowCircles();
                });
            }
        }

        #endregion
    }
}
