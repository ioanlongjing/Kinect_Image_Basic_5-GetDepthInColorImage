using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text;
using Microsoft.Kinect;

namespace GetDepthInColorImage
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        private DepthImagePoint[] depthCoordinates;

        private int cx, cy; //mouse position in color image

        private int colorWidth;
        private int colorHeight;

        private int depthWidth;
        private int depthHeight;
        
        private FileStream fs;
        StreamWriter sw;

        public MainWindow()
        {
            InitializeComponent();

            fs = new FileStream("depthInfo.txt", FileMode.Create);
            sw = new StreamWriter(fs, Encoding.Default);
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorFormat);
                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.colorWidth = this.sensor.ColorStream.FrameWidth;
                this.colorHeight = this.sensor.ColorStream.FrameHeight;
                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.image1.Source = this.colorBitmap;

                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);
                //this.sensor.DepthStream.Range = DepthRange.Default;
                this.sensor.DepthStream.Range = DepthRange.Near;

                this.depthWidth = this.sensor.DepthStream.FrameWidth;
                this.depthHeight = this.sensor.DepthStream.FrameHeight;
                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.depthCoordinates = new DepthImagePoint[this.colorWidth * this.colorHeight];

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.AllFramesReady += this.SensorAllFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusLbl.Content = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's AllFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }

            bool depthReceived = false;
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    depthReceived = true;
                }
            }
            
            if (depthReceived)
            {
                //to get the correspong coordinate in depth image for each color pixel
                this.sensor.CoordinateMapper.MapColorFrameToDepthFrame(ColorFormat, 
                                                                       DepthFormat, 
                                                                       depthPixels, 
                                                                       depthCoordinates);
                if ((cx >= 0 && cx < colorWidth)&&(cy >=0 && cy < colorHeight))
                {
                    int colorIndex = cx + cy * colorWidth;
                    int depthIndex = depthCoordinates[colorIndex].X + depthCoordinates[colorIndex].Y * depthWidth;
                    if (depthIndex >= 0 && depthIndex < depthWidth * depthHeight)
                    {
                        short depth = depthPixels[depthIndex].Depth;
                        mousePosLbl.Content = "(" + cx + "," + cy + ")" + depth.ToString();
                    }
                }
            }            
        }

        private void image1_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point p = e.GetPosition(image1);
            cx = (int)(p.X + 0.5);
            cy = (int)(p.Y + 0.5);
            //mousePosLbl.Content = "(" + cx + "," + cy + ")"; 
            
        }

        private void image1_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            statusLbl.Content = "get depth info";
            int cx = (int)Canvas.GetLeft(faceRect);
            int cy = (int)Canvas.GetTop(faceRect);
            int width = (int)faceRect.Width;
            int height = (int)faceRect.Height;

            for (int r = cy; r < cy + height; r++)
            {
                for (int c = cx; c < cx + width; c++)
                {
                    int colorIndex = c + r * colorWidth;
                    int depthIndex = depthCoordinates[colorIndex].X + depthCoordinates[colorIndex].Y * depthWidth;
                    if (depthIndex >= 0 && depthIndex < depthWidth * depthHeight)
                    {
                        short depth = depthPixels[depthIndex].Depth;
                        sw.Write("{0} ", depth.ToString());
                    }
                    else
                        sw.Write("0 ");
                }
                sw.WriteLine();
            }
            sw.Close();
            fs.Close();          
        }        
    }
}
