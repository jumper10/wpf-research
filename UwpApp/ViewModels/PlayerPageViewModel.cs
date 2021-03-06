﻿using GalaSoft.MvvmLight.Threading;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UwpApp.Views;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.FaceAnalysis;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace UwpApp.ViewModels
{
    public class PlayerPageViewModel: UWPViewModelBase
    {
        public override void OnLoaded(object obj = null)
        {
            base.OnLoaded(obj);
        }
        public override void UnLoaded()
        {
            base.UnLoaded();
            if (_player != null)
            {
                _player.Dispose();
                _player = null;
            }
            if (frameProcessingTimer != null)
            {
                frameProcessingTimer.Cancel();
            }
            frameProcessingTimer = null;
        }

        #region Player
        private Canvas _renderCanvas;
        MediaPlayer _player;
        private PlayerPage _playerView;
        Size _renderSize;
        public async void InitPlayer(PlayerPage render, string filePath)
        {
            if (_player == null) _player = new MediaPlayer();

            _playerView = render;
            _renderCanvas = render.RenderCanvas;

            var file = await StorageFile.GetFileFromPathAsync(filePath);

            var mediaSource = MediaSource.CreateFromStorageFile(file);

            _player.Source = mediaSource;

            //Compositor compositor =  InitPlayerRender(_player);
            //if (compositor != null)
            //{

            //}

            _player.IsVideoFrameServerEnabled = true;
            _player.VideoFrameAvailable += _player_VideoFrameAvailable;           
            StartFaceTracker();
            _player.Play();
        }
        volatile SoftwareBitmap _LastFrame;
        SoftwareBitmap _frameDest;
        SemaphoreSlim _frameSemaphore = new SemaphoreSlim(1);
        CanvasImageSource canvasImageSource;

        SoftwareBitmap frameServerDest;

        private async void _player_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            // if (!_frameSemaphore.Wait(0)) return;

            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

            await _playerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,() =>
            {
                if (frameServerDest == null)
                {
                    _renderSize = new Size(_renderCanvas.ActualWidth, _renderCanvas.ActualHeight);
                    frameServerDest = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)_renderSize.Width, (int)_renderSize.Height, BitmapAlphaMode.Ignore);
                }
                if (canvasImageSource == null)
                {
                    canvasImageSource = new CanvasImageSource(canvasDevice, (int)_renderSize.Width, (int)_renderSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 
              
                    _playerView.RenderCanvas.Background = new ImageBrush { ImageSource = canvasImageSource };
                }
                using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest))
                using (CanvasDrawingSession ds = canvasImageSource.CreateDrawingSession(Windows.UI.Colors.Black))
                {


                    _player.CopyFrameToVideoSurface(inputBitmap);

                    //var gaussianBlurEffect = new GaussianBlurEffect
                    //{
                    //    Source = inputBitmap,
                    //   // BlurAmount = 5f,
                    //    Optimization = EffectOptimization.Quality
                    //};

                    ds.DrawImage(inputBitmap);

                    if (_LastFrame != null)
                    {
                        _LastFrame.Dispose();
                    }
                    var map = SoftwareBitmap.CreateCopyFromSurfaceAsync(inputBitmap);
                    map.Completed = (obj,state) => {
                        var mapr = obj.GetResults();
                        if (mapr != null)
                        {
                            _LastFrame = (SoftwareBitmap.Convert(mapr, BitmapPixelFormat.Gray8));
                            mapr.Dispose();
                            mapr = null;
                        }
                    };
                }
            });
        }
        private Compositor InitPlayerRender(MediaPlayer mediaPlayer)
        {
            
            if (mediaPlayer == null || _renderCanvas == null) return null;
            _renderSize = new Size(_renderCanvas.ActualWidth, _renderCanvas.ActualHeight);
            mediaPlayer.SetSurfaceSize(_renderSize);
            var compositor = Window.Current.Compositor;// ElementCompositionPreview.GetElementVisual(_playerView).Compositor;
            MediaPlayerSurface surface = mediaPlayer.GetSurface(compositor);

            SpriteVisual spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Size =
                new System.Numerics.Vector2((float)_renderSize.Width, (float)_renderSize.Height);

            CompositionBrush brush = compositor.CreateSurfaceBrush(surface.CompositionSurface);
            spriteVisual.Brush = brush;

            ContainerVisual container = compositor.CreateContainerVisual();
            container.Children.InsertAtTop(spriteVisual);

            ElementCompositionPreview.SetElementChildVisual(_renderCanvas, container);

            return compositor;
        }
        #endregion
        #region FaceDetector
        private ThreadPoolTimer frameProcessingTimer;
        public bool FaceTracked { get; set; } = true;
        IList<DetectedFace> detectedFaces;
        FaceTracker faceTracker;
        FaceDetector faceDetector;
        SemaphoreSlim _frameProcessingSemaphore = new SemaphoreSlim(1);
        Brush _fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        Brush _lineBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        double _LineThickness = 2;

        private async void StartFaceTracker()
        {
            if (faceTracker == null) faceTracker = await FaceTracker.CreateAsync();
            if (faceDetector == null) faceDetector =await FaceDetector.CreateAsync();
            faceDetector.MinDetectableFaceSize = new BitmapSize { Height = 5, Width = 5 };
            TimeSpan timerInterval = TimeSpan.FromMilliseconds(66);
            frameProcessingTimer = Windows.System.Threading.ThreadPoolTimer.CreatePeriodicTimer(new Windows.System.Threading.TimerElapsedHandler(ProcessCurrentVideoFrame), timerInterval);
        }
        
        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {
            if (_LastFrame == null) return;

            if (!_frameProcessingSemaphore.Wait(0)) return;
            try
            {
                if (!FaceDetector.IsBitmapPixelFormatSupported(_LastFrame.BitmapPixelFormat)) return;

                try
                {
                    if (!_frameSemaphore.Wait(0)) return;
                    detectedFaces = await faceDetector.DetectFacesAsync(_LastFrame);

                    _LastFrame.Dispose();
                    _LastFrame = null;
                    await DispatcherHelper.RunAsync(() =>
                    {
                        UpdateFacesVisualization(detectedFaces, _renderSize);
                    });
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    _frameSemaphore.Release();
                }
               
            }
            catch(Exception ex)
            {

            }
            finally
            {
                _frameProcessingSemaphore.Release();
            }
        }

        private void UpdateFacesVisualization(IList<DetectedFace> faces, Size viewSize)
        {
            _playerView.RenderCanvas.Children.Clear();
            if (faces == null) return;
            if (viewSize.Height != 0 && viewSize.Width != 0 && faces.Any())
            {
                foreach (var face in faces)
                {
                    Rectangle box = new Rectangle();
                    box.Width = face.FaceBox.Width;
                    box.Height = face.FaceBox.Height;

                    box.Fill = _fillBrush;
                    box.Stroke = _lineBrush;
                    box.StrokeThickness = _LineThickness;
                    box.Margin = new Thickness(face.FaceBox.X, face.FaceBox.Y, 0, 0);

                   _playerView.RenderCanvas.Children.Add(box);
                }
            }
        }
        #endregion
    }
}
