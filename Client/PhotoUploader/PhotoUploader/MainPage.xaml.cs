using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace PhotoUploader
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private string deviceId;
        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            FixDeviceId();
            await StartPhotoUpload();
        }

        MediaCapture mediaCaptureManager = null;
        string containerName = "photos";
        DispatcherTimer photoUploadTimer;
        int photoUploadIntervalSec = 20;

        private async Task StartPhotoUpload()
        {
            mediaCaptureManager = new MediaCapture();
            try
            {
                await InitializeCloudPhotoContainer();
                await mediaCaptureManager.InitializeAsync();
                previewElement.Source = mediaCaptureManager;
                await mediaCaptureManager.StartPreviewAsync();
                photoUploadTimer = new DispatcherTimer();
                photoUploadTimer.Interval = TimeSpan.FromSeconds(photoUploadIntervalSec);
                photoUploadTimer.Tick += (s, o) =>
                {
                    StartUploadPhoto();
                };
                photoUploadTimer.Start();
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Exception Happen in initialize photo uploading - " + ex.Message);
            }
        }

        CloudBlobContainer photoContainer;
        private string storageAccountName = "[account-name]";
        private string storageKey = "[storage-key]";
        private async Task InitializeCloudPhotoContainer()
        {
            var storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=" + storageAccountName + ";AccountKey=" + storageKey;
            var cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var blobClient = cloudStorageAccount.CreateCloudBlobClient();
            photoContainer = blobClient.GetContainerReference(containerName);
            await photoContainer.CreateIfNotExistsAsync();
        }

        StorageFile photoStorageFile;
        string capturedPhotoFile = "captured.jpg";

        private async Task UploadPhoto()
        {
            photoStorageFile = await Windows.Storage.KnownFolders.PicturesLibrary.CreateFileAsync(capturedPhotoFile, CreationCollisionOption.ReplaceExisting);
            var imageProperties = ImageEncodingProperties.CreateJpeg();
            try
            {
                await mediaCaptureManager.CapturePhotoToStorageFileAsync(imageProperties, photoStorageFile);
                var fileName = "device-" + deviceId + "_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss") + "_Pro.jpg";
                var blockBlob = photoContainer.GetBlockBlobReference(fileName);
                await blockBlob.UploadFromFileAsync(photoStorageFile);
                Debug.WriteLine(string.Format("Uploaded: {0} at {1}", fileName, DateTime.Now.ToString("yyyy/MM/dd - hh:mm:ss")));
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
        }

        private void StartUploadPhoto()
        {
            photoUploadTimer.Stop();
            if (countdownTimer != null)
            {
                countdownTimer.Stop();
            }
            countdownIndex = countdownIntervalSec;
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += async (o, s) =>
            {
                tbStatus.Text = countdownIndex.ToString();
                if (countdownIndex == 0)
                {
                    countdownTimer.Stop();
                    await UploadPhoto();
                    tbStatus.Visibility = Visibility.Collapsed;
                    photoUploadTimer.Start();
                }
                countdownIndex--;
            };
            tbStatus.Text = countdownIndex.ToString();
            tbStatus.Visibility = Visibility.Visible;
            countdownTimer.Start();
        }

        DispatcherTimer countdownTimer;
        int countdownIndex = 5;
        int countdownIntervalSec = 5;

        private void FixDeviceId()
        {
            foreach (var hn in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
            {
                IPAddress ipAddr;
                if (!hn.DisplayName.EndsWith(".local") && !IPAddress.TryParse(hn.DisplayName, out ipAddr))
                {
                    deviceId = hn.DisplayName;
                    break;
                }
            }
        }

    }
}
