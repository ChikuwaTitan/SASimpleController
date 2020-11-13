/*!
 * MainPage.xaml.cs
 *
 * Copyright (c) 2020 ChikuwaTitan
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Advertisement;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Popups;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using System.Reflection.Metadata;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using Windows.Storage.Pickers;


// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace SASimpleController
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private BluetoothLEAdvertisementWatcher sAdvWatcher;
        GattDeviceService sMotorService_CycSA;
        GattDeviceService sMotorService_UfoSA;
        Guid sServiceUuid_CycSA = new Guid("40EE1111-63EC-4B7F-8CE7-712EFD55B90E");
        Guid sServiceUuid_UfoSA = new Guid("40EE1111-63EC-4B7F-8CE7-712EFD55B90E");

        GattCharacteristic sGattMotorChara_CycSA;
        GattCharacteristic sGattMotorChara_UfoSA;
        Guid sMotorCharaUuid_CycSA = new Guid("40EE2222-63EC-4B7F-8CE7-712EFD55B90E");
        Guid sMotorCharaUuid_UfoSA = new Guid("40EE2222-63EC-4B7F-8CE7-712EFD55B90E");


        const string TGT_DEV_NAME_CYCSA = "CycSA";
        const string TGT_DEV_NAME_UFOSA = "UFOSA";
        BluetoothLEDevice sConnectDevice_CycSA;
        BluetoothLEDevice sConnectDevice_UfoSA;

        private Boolean sIsVideoFile;
        private DispatcherTimer sTimer = new DispatcherTimer();
        private DispatcherTimer sAdvTimer = new DispatcherTimer();

        private Object lockObject = new Object();

        private RegisterProgressDialog sProgressDialog = new RegisterProgressDialog();

        public List<Windows.Storage.StorageFile> FileList;

        private struct ST_CSV_ROW
        {
            public double time;
            public byte direction;
            public byte speed;
        };

        List<ST_CSV_ROW> sCycCsvFile;
        List<ST_CSV_ROW> sUfoCsvFile;
        int sCycCsvRowIndex;
        int sUfoCsvRowIndex;
        const int CSV_COL_TIME = 0;
        const int CSV_COL_DIRECTION = 1;
        const int CSV_COL_SPEED = 2;


        XElement sPlayList;
        string sXmlFilePath;

        public MainPage()
        {
            this.InitializeComponent();

            this.sAdvWatcher = new BluetoothLEAdvertisementWatcher();

            sTimer.Interval = TimeSpan.FromMilliseconds(100);
            sTimer.Tick += TICK_Process;
            sXmlFilePath = Windows.Storage.ApplicationData.Current.LocalFolder.Path + @"\playlist.xml";
            ReadPlayList();
        }

        private void ReadPlayList() {

            XElement playList;
            try
            {
                playList = XElement.Load(sXmlFilePath);
            }
            catch {
                playList = null;
            }

            CMB_PlayList.Items.Clear();

            if (playList != null)
            {
                /*ファイル読み込み処理*/
                sPlayList = playList;

                foreach (XElement row in sPlayList.Elements("PlayInfo"))
                {
                    string playInfoName = row.Element("Name").Value;
                    CMB_PlayList.Items.Add(playInfoName);
                }
                CMB_PlayList.IsEnabled = true;


            }
            else {
                /*ファイル作成*/
                sPlayList = new XElement("PlayList");

                sPlayList.Save(sXmlFilePath);
            }

        }


        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        ///
        /// We will enable/disable parts of the UI if the device doesn't support it.
        /// </summary>
        /// <param name="eventArgs">Event data that describes how this page was reached. The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {


        }


        /// <summary>
        /// Invoked immediately before the Page is unloaded and is no longer the current source of a parent Frame.
        /// </summary>
        /// <param name="e">
        /// Event data that can be examined by overriding code. The event data is representative
        /// of the navigation that will unload the current Page unless canceled. The
        /// navigation can potentially be canceled by setting Cancel.
        /// </param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Remove local suspension handlers from the App since this page is no longer active.
            App.Current.Suspending -= App_Suspending;
            App.Current.Resuming -= App_Resuming;


            base.OnNavigatingFrom(e);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {

        }

        /// <summary>
        /// Invoked when application execution is being resumed.
        /// </summary>
        /// <param name="sender">The source of the resume request.</param>
        /// <param name="e"></param>
        private void App_Resuming(object sender, object e)
        {

        }



        /// <summary>
        /// Invoked as an event handler when an advertisement is received.
        /// </summary>
        /// <param name="watcher">Instance of watcher that triggered the event.</param>
        /// <param name="eventArgs">Event data containing information about the advertisement event.</param>
        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            if (eventArgs.Advertisement.LocalName == TGT_DEV_NAME_CYCSA)
            {
                sConnectDevice_CycSA = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

                GattDeviceServicesResult gattDeviceServicesResult = await sConnectDevice_CycSA.GetGattServicesAsync();

                sConnectDevice_CycSA.ConnectionStatusChanged += Dev_ConnectionStatusChanged;

                for (int i = 0; i < gattDeviceServicesResult.Services.Count; i++)
                {
                    if (gattDeviceServicesResult.Services[i].Uuid == sServiceUuid_CycSA)
                    {
                        sMotorService_CycSA = gattDeviceServicesResult.Services[i];
                    }
                }

                GattCharacteristicsResult gattCharacteristics = await sMotorService_CycSA.GetCharacteristicsAsync();
                for (int i = 0; i < gattCharacteristics.Characteristics.Count; i++)
                {
                    Guid uuid = gattCharacteristics.Characteristics[i].Uuid;
                    if (uuid == sMotorCharaUuid_CycSA)
                    {
                        sGattMotorChara_CycSA = gattCharacteristics.Characteristics[i];
                    }
                    else
                    {
                    }
                }

                /*画面表示をEnabla*/
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    BT_CycConnect.Content = "CycConnected";
                    sProgressDialog.Hide();
                    BT_CycConnect.IsEnabled = false;
                    sAdvTimer.Stop();
                });
                AdvertisementStop();
            }

            if (eventArgs.Advertisement.LocalName == TGT_DEV_NAME_UFOSA)
            {
                sConnectDevice_UfoSA = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

                GattDeviceServicesResult gattDeviceServicesResult = await sConnectDevice_UfoSA.GetGattServicesAsync();

                sConnectDevice_UfoSA.ConnectionStatusChanged += Dev_ConnectionStatusChanged;

                for (int i = 0; i < gattDeviceServicesResult.Services.Count; i++)
                {
                    if (gattDeviceServicesResult.Services[i].Uuid == sServiceUuid_UfoSA)
                    {
                        sMotorService_UfoSA = gattDeviceServicesResult.Services[i];
                    }
                }

                GattCharacteristicsResult gattCharacteristics = await sMotorService_UfoSA.GetCharacteristicsAsync();
                for (int i = 0; i < gattCharacteristics.Characteristics.Count; i++)
                {
                    Guid uuid = gattCharacteristics.Characteristics[i].Uuid;
                    if (uuid == sMotorCharaUuid_UfoSA)
                    {
                        sGattMotorChara_UfoSA = gattCharacteristics.Characteristics[i];
                    }
                    else
                    {
                    }
                }

                /*画面表示をEnabla*/
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    BT_UfoConnect.Content = "UfoConnected";
                    sProgressDialog.Hide();
                    BT_UfoConnect.IsEnabled = false;
                    sAdvTimer.Stop();
                });
                AdvertisementStop();
            }

        }

        private async void Dev_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                if (sender.DeviceInformation.Name == TGT_DEV_NAME_CYCSA)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        BT_CycConnect.Content = "CycConnect";

                        BT_CycConnect.IsEnabled = true;
                    });
                }
                if (sender.DeviceInformation.Name == TGT_DEV_NAME_UFOSA)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        BT_UfoConnect.Content = "UfoConnect";
                        BT_UfoConnect.IsEnabled = true;
                    });
                }
            }
        }

        private async void BLE_MotorDataSend_CycSA(byte mode, byte speed)
        {
            if (sGattMotorChara_CycSA != null)
            {
                byte motor;
                motor = (byte)((mode << 7) & 0x80);
                motor = (byte)(motor | speed);
                byte[] array = { 0x01, 0x01, motor };

                var writer = new DataWriter();
                writer.WriteBytes(array);
                try
                {
                    GattCommunicationStatus result = await sGattMotorChara_CycSA.WriteValueAsync(writer.DetachBuffer());
                }
                catch
                {

                }
            }
        }

        private async void BLE_MotorDataSend_UfoSA(byte mode, byte speed)
        {
            if (sGattMotorChara_UfoSA != null)
            {
                byte motor;
                motor = (byte)((mode << 7) & 0x80);
                motor = (byte)(motor | speed);
                byte[] array = { 0x02, 0x01, motor };

                var writer = new DataWriter();
                writer.WriteBytes(array);
                try
                {
                    GattCommunicationStatus result = await sGattMotorChara_UfoSA.WriteValueAsync(writer.DetachBuffer());
                }
                catch
                {

                }
            }

        }

        private async void StopButtonProcess(GattCharacteristic gatt, GattValueChangedEventArgs args)
        {
            GattReadResult result = await gatt.ReadValueAsync();
            byte readVal = result.Value.GetByte(0);
            if (readVal == 1)
            {
                /*Pause処理*/
                PauseProcess();
                BLE_MotorDataSend_CycSA(3, 0);
            }
        }

        /// <summary>
        /// Invoked as an event handler when the watcher is stopped or aborted.
        /// </summary>
        /// <param name="watcher">Instance of watcher that triggered the event.</param>
        /// <param name="eventArgs">Event data containing information about why the watcher stopped or aborted.</param>
        private async void OnAdvertisementWatcherStopped(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementWatcherStoppedEventArgs eventArgs)
        {
            // Notify the user that the watcher was stopped
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
            });
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
        }

        private async void BT_CycCsvOpen_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();

            // 選択可能な拡張子を追加
            filePicker.FileTypeFilter.Add(".csv");

            Windows.Storage.StorageFile file = await filePicker.PickSingleFileAsync();

            if (file == null)
            {
                return;
            }

            TB_CycCsvPath.Text = file.Path.ToString();
            sCycCsvFile = await GetCsvData(file);
            sCycCsvRowIndex = 0;
        }

        private async void BT_UfoCsvOpen_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();

            // 選択可能な拡張子を追加
            filePicker.FileTypeFilter.Add(".csv");

            Windows.Storage.StorageFile file = await filePicker.PickSingleFileAsync();

            if (file == null)
            {
                return;
            }

            TB_UfoCsvPath.Text = file.Path.ToString();
            sUfoCsvFile = await GetCsvData(file);
            sUfoCsvRowIndex = 0;
        }

        private async Task<List<ST_CSV_ROW>> GetCsvData(Windows.Storage.StorageFile file)
        {            // csvファイルを開く
            List<ST_CSV_ROW> tempcsvFile = new List<ST_CSV_ROW>();
            using (StreamReader sr = new System.IO.StreamReader(await file.OpenStreamForReadAsync()))
            {
                // ストリームの末尾まで繰り返す
                while (!sr.EndOfStream)
                {
                    // ファイルから一行読み込む
                    var line = sr.ReadLine();
                    // 読み込んだ一行をカンマ毎に分けて配列に格納する
                    var values = line.Split(',');
                    if (values.Length >= 3)
                    {
                        try
                        {
                            ST_CSV_ROW csvRow;
                            csvRow.time = double.Parse(values[0].ToString()) * 100;/*msで格納*/
                            csvRow.direction = byte.Parse(values[1].ToString());
                            csvRow.speed = byte.Parse(values[2].ToString());

                            // 出力する
                            tempcsvFile.Add(csvRow);
                        }
                        catch
                        {
                            /*変換できない文字などがあればスキップ*/
                        }
                    }
                }
            }
            return tempcsvFile;
        }


        private void TICK_Process(object sender, object e)
        {
            double milSec = 0;
            lock (lockObject)
            {
                milSec = mediaElement.Position.TotalMilliseconds;
                SL_Time.Value = milSec;
                TB_NowTime.Text = GetTimeSpanStr((ulong)milSec);
            }
            double posTime = milSec;
            if ((sCycCsvFile != null))
            {
                if (sCycCsvFile.Count <= sCycCsvRowIndex)
                {
                    /*do nothing*/
                }
                else
                {
                    if (sCycCsvFile[sCycCsvRowIndex].time <= posTime)
                    {
                        byte speed = sCycCsvFile[sCycCsvRowIndex].speed;
                        byte mode = sCycCsvFile[sCycCsvRowIndex].direction;
                        /*arduinoに送信*/
                        BLE_MotorDataSend_CycSA(mode, speed);

                        /*Indexを次の行に移す*/
                        sCycCsvRowIndex++;
                    }
                    else
                    {
                        /*do nothing*/
                    }
                }
            }
            if ((sUfoCsvFile != null))
            {
                if (sUfoCsvFile.Count <= sUfoCsvRowIndex)
                {
                    /*do nothing*/
                }
                else
                {
                    if (sUfoCsvFile[sUfoCsvRowIndex].time <= posTime)
                    {
                        byte speed = sUfoCsvFile[sUfoCsvRowIndex].speed;
                        byte mode = sUfoCsvFile[sUfoCsvRowIndex].direction;
                        /*arduinoに送信*/
                        BLE_MotorDataSend_UfoSA(mode, speed);

                        /*Indexを次の行に移す*/
                        sUfoCsvRowIndex++;
                    }
                    else
                    {
                        /*do nothing*/
                    }
                }
            }

        }

        private void BT_Resume_Click(object sender, RoutedEventArgs e)
        {
            if (sTimer.IsEnabled != true)
            {
                ResumeProcess();
            }
            else
            {
                this.mediaElement.IsFullWindow = false;

                PauseProcess();
            }
        }

        private void ResumeProcess()
        {
            SeekBar_Resume();
            sTimer.Start();
            if (sIsVideoFile)
            {
                if (CB_FULLSCREEN.IsChecked == true)
                {
                    this.mediaElement.IsFullWindow = true;
                }
            }
            this.mediaElement.Play();

            TB_CycCsvPath.IsEnabled = false;
            TB_UfoCsvPath.IsEnabled = false;

            BT_Resume.Content = "||";
        }

        private void PauseProcess()
        {
            this.mediaElement.IsFullWindow = false;
            this.mediaElement.Pause();
            TB_CycCsvPath.IsEnabled = true;
            TB_UfoCsvPath.IsEnabled = true;
            BT_Resume.Content = "▶";
            sTimer.Stop();
            BLE_MotorDataSend_CycSA(0, 0);
            BLE_MotorDataSend_UfoSA(0, 0);
        }

        private void StopProcess()
        {
            this.mediaElement.IsFullWindow = false;
            sTimer.Stop();
            this.mediaElement.Stop();
            TB_CycCsvPath.IsEnabled = true;
            TB_UfoCsvPath.IsEnabled = true;
            BT_Resume.Content = "▶";

            sCycCsvRowIndex = 0;
            sUfoCsvRowIndex = 0;
            BLE_MotorDataSend_CycSA(0, 0);
            BLE_MotorDataSend_UfoSA(0, 0);
            lock (lockObject)
            {
                SL_Time.Value = 0;
                mediaElement.Position = GetTimeSpan(0);
                TB_NowTime.Text = GetTimeSpanStr(0);
            }
        }

        private void BT_Stop_Click(object sender, RoutedEventArgs e)
        {
            StopProcess();


        }


        bool IsTimer = false;
        private void SL_Time_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            SeekBar_Resume();
            if (IsTimer == true)
            {
                sTimer.Start();
            }

        }

        private void SL_Time_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            if (sTimer.IsEnabled != false)
            {
                IsTimer = true;
            }
            sTimer.Stop();

        }


        private void SeekBar_Resume()
        {
            UInt64 miliSec = 0;
            lock (lockObject)
            {
                miliSec = (ulong)SL_Time.Value;
                TB_NowTime.Text = GetTimeSpanStr(miliSec);
                mediaElement.Position = GetTimeSpan(miliSec);
            }

            if (sCycCsvFile != null)
            {
                for (int i = 0; i < sCycCsvFile.Count; i++)
                {
                    if (sCycCsvFile[i].time >= miliSec)
                    {
                        if (i == 0)
                        {
                            sCycCsvRowIndex = 0;
                        }
                        else
                        {
                            sCycCsvRowIndex = i - 1;
                        }
                        break;

                    }
                }
            }
            if (sUfoCsvFile != null)
            {
                for (int i = 0; i < sUfoCsvFile.Count; i++)
                {
                    if (sUfoCsvFile[i].time >= miliSec)
                    {
                        if (i == 0)
                        {
                            sUfoCsvRowIndex = 0;
                        }
                        else
                        {
                            sUfoCsvRowIndex = i - 1;
                        }
                        break;

                    }
                }
            }
        }

        private void SL_Time_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TapProcess();
        }


        private void BT_MediaOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenMediaFile();
        }

        private async void OpenMediaFile() {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();

            filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;

            // 選択可能な拡張子を追加
            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wav");
            filePicker.FileTypeFilter.Add(".mp4");
            filePicker.FileTypeFilter.Add(".wmv");
            filePicker.FileTypeFilter.Add(".avi");
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFile mediaFile = await filePicker.PickSingleFileAsync();


            if (mediaFile != null)
            {
                SetMediaFile(mediaFile);
            }

        }

        private async void SetMediaFile(Windows.Storage.StorageFile mediaFile) {
            const string fileDurationProperty = "System.Media.Duration";

            if (mediaFile.ContentType.Contains("video"))
            {
                sIsVideoFile = true;
            }
            else if (mediaFile.ContentType.Contains("audio"))
            {
                sIsVideoFile = false;
            }
            else
            {
                return;
            }

            var stream = await mediaFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
            mediaElement.SetSource(stream, mediaFile.ContentType);

            // Get file's basic properties.
            var propertyNames = new List<string>();
            propertyNames.Add(fileDurationProperty);
            Windows.Storage.FileProperties.BasicProperties basicProperties =
                await mediaFile.GetBasicPropertiesAsync();
            IDictionary<string, object> extraProperties = await mediaFile.Properties.RetrievePropertiesAsync(propertyNames);
            UInt64 durationTime = (UInt64)extraProperties[fileDurationProperty];
            durationTime = durationTime / 10000;/*msに変換*/
            TB_MaxTime.Text = GetTimeSpanStr(durationTime);
            BT_Resume.IsEnabled = true;
            BT_Stop.IsEnabled = true;
            SL_Time.IsEnabled = true;
            SL_Time.Maximum = durationTime;

            TB_MediaPath.Text = mediaFile.Path.ToString();
        }

        private TimeSpan GetTimeSpan(UInt64 time)
        {
            int hour = (int)(time / 3600000);/**/
            time = time % 3600000;
            int minite = (int)(time / 60000);
            time = time % 60000;
            int second = (int)(time / 1000);
            TimeSpan ret = new TimeSpan(hour, minite, second);
            return ret;
        }

        private string GetTimeSpanStr(UInt64 time)
        {
            TimeSpan ret = GetTimeSpan(time);
            return ret.ToString(@"\ hh\:mm\:ss");
        }


        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            StopProcess();
        }

        private void TapProcess()
        {
            if (sTimer.IsEnabled != false)
            {
                IsTimer = true;
            }

            sTimer.Stop();
            SeekBar_Resume();

            if (IsTimer == true)
            {
                IsTimer = false;
                sTimer.Start();
            }
        }

        private void SL_Time_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }

        private void SL_Time_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingRoutedEventArgs e)
        {

        }

        private void SL_Time_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {

        }


        private void SL_Time_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            TapProcess();
        }

        private void SL_Time_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
        {
            TapProcess();
        }

        private void MediaElement_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sTimer.IsEnabled != true)
            {
                ResumeProcess();
            }
            else
            {
                PauseProcess();
            }
        }

        private async void BT_CycConnect_Click(object sender, RoutedEventArgs e)
        {

            AdvertisementStart(TGT_DEV_NAME_CYCSA);
            await sProgressDialog.ShowAsync();
        }



        private async void BT_UfoConnect_Click(object sender, RoutedEventArgs e)
        {
            AdvertisementStart(TGT_DEV_NAME_UFOSA);
            await sProgressDialog.ShowAsync();
        }

        private void AdvertisementStart(string devName) {
            this.sAdvWatcher.AdvertisementFilter.Advertisement.LocalName = devName;
            this.sAdvWatcher.ScanningMode = BluetoothLEScanningMode.Active;
            this.sAdvWatcher.SignalStrengthFilter.InRangeThresholdInDBm = -100;
            this.sAdvWatcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -105;
            this.sAdvWatcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(2000);
            // Attach a handler to process the received advertisement. 
            // The watcher cannot be started without a Received handler attached
            sAdvWatcher.Received += OnAdvertisementReceived;

            // Attach a handler to process watcher stopping due to various conditions,
            // such as the Bluetooth radio turning off or the Stop method was called
            sAdvWatcher.Stopped += OnAdvertisementWatcherStopped;

            // Attach handlers for suspension to stop the watcher when the App is suspended.
            App.Current.Suspending += App_Suspending;
            App.Current.Resuming += App_Resuming;

            sAdvWatcher.Start();

            sAdvTimer.Interval = TimeSpan.FromSeconds(10);
            sAdvTimer.Tick += AdvertisementTimeout;
            sAdvTimer.Start();
        }

        private void AdvertisementTimeout(object sender, object e)
        {
            AdvertisementStop();
            sAdvTimer.Stop();
            sProgressDialog.Hide();
        }

        private void AdvertisementStop()
        {
            sAdvWatcher.Stop();
        }

        private async void Grid_DragOver(object sender, DragEventArgs e)
        {

            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            List<Windows.Storage.IStorageFile> fileList = new List<Windows.Storage.IStorageFile>();
            // ファイルの場合
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // ファイルのパスをListViewに表示する
                var items = await e.DataView.GetStorageItemsAsync();

                if (items.Count > 1)
                {

                }
                else {
                    if (!items[0].Attributes.HasFlag(Windows.Storage.FileAttributes.Directory))
                    {
                        Windows.Storage.StorageFile file = (Windows.Storage.StorageFile)items[0];
                        if (file.FileType == ".csv")
                        {
                            /*CycかUfoか選ばせる*/
                            var dlg = new MessageDialog(file.DisplayName + "がどのCSVファイルか選んでください", "CSVファイルの選択");
                            dlg.Commands.Add(new UICommand("Cyc SA", null, 0));
                            dlg.Commands.Add(new UICommand("UFO SA", null, 1));
                            dlg.Commands.Add(new UICommand("キャンセル", null, 2));

                            var selectedCommand = await dlg.ShowAsync();
                            var result = (int)selectedCommand.Id;
                            if (result == 0) {

                                TB_CycCsvPath.Text = file.Path.ToString();
                                sCycCsvFile = await GetCsvData(file);
                                sCycCsvRowIndex = 0;
                            }
                            else if (result == 1)
                            {

                                TB_UfoCsvPath.Text = file.Path.ToString();
                                sUfoCsvFile = await GetCsvData(file);
                                sUfoCsvRowIndex = 0;
                            }
                            else {
                            }
                        }
                        else if (file.FileType == ".lnk") {
                            /*ショートカットファイルにアクセスする方法がない。*/
                        }
                        else {
                            /*メディアファイルにセット*/
                            SetMediaFile(file);
                        }
                    }
                    else {
                        /*フォルダをドラッグドロップ*/
                        listBox.Items.Clear();
                        Windows.Storage.StorageFolder folder = (Windows.Storage.StorageFolder)items[0];
                        FileList = new List<Windows.Storage.StorageFile>();


                        var files = await folder.GetFilesAsync();
                        for (int i = 0; i < files.Count; i++) {
                            FileList.Add(files[i]);
                            if ((files[i].ContentType.Contains("video"))
                             || (files[i].ContentType.Contains("audio"))) {
                                listBox.Items.Add(files[i]);
                            }
                        }
                        if (files.Count ==0 ) {
                            return;
                        }
                        if (listBox.Items.Count > 0) {
                            DLG_FileSelect.Title = "動画または音声ファイルを選択してください";
                            sSelectMode = FileSelectMode.MEDIA_FILE;
                            var test = await DLG_FileSelect.ShowAsync();
                        }
                        else {
                            DLG_FileSelect.Title = "サイクロンSA用のCSVファイルを選択してください";
                            for (int i = 0; i < FileList.Count; i++)
                            {
                                if (FileList[i].FileType == ".csv")
                                {
                                    listBox.Items.Add(FileList[i]);
                                }
                            }
                            sSelectMode = FileSelectMode.CYC_CSV;
                            var test = await DLG_FileSelect.ShowAsync();
                        } 
                    }
                }
            }

        }

        private void DLG_FileSelect_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            /*キャンセル*/
        }

        enum FileSelectMode {
            MEDIA_FILE = 0,
            CYC_CSV = 1,
            UFO_CSV = 2
        }
        FileSelectMode sSelectMode = FileSelectMode.MEDIA_FILE;
        private void BT_FileSelectOK_Click(object sender, RoutedEventArgs e)
        {
            FileSelectSequence();
        }

        private async void FileSelectSequence() {

            if (sSelectMode == FileSelectMode.MEDIA_FILE)
            {
                /*セット処理*/
                Windows.Storage.StorageFile file = (Windows.Storage.StorageFile)listBox.SelectedItem;
                SetMediaFile(file);

                /*画面更新*/
                sSelectMode = FileSelectMode.CYC_CSV;
                listBox.Items.Clear();
                DLG_FileSelect.Title = "サイクロンSA用のCSVファイルを選択してください";
                for (int i = 0; i < FileList.Count; i++)
                {
                    if (FileList[i].FileType == ".csv")
                    {
                        listBox.Items.Add(FileList[i]);
                    }
                }
            }
            else if (sSelectMode == FileSelectMode.CYC_CSV)
            {
                /*セット処理*/
                Windows.Storage.StorageFile file = (Windows.Storage.StorageFile)listBox.SelectedItem;
                TB_CycCsvPath.Text = file.Path.ToString();
                sCycCsvFile = await GetCsvData(file);
                sCycCsvRowIndex = 0;
                /*画面更新処理*/
                DLG_FileSelect.Title = "UFO SA用のCSVファイルを選択してください";
                sSelectMode = FileSelectMode.UFO_CSV;
            }
            else if (sSelectMode == FileSelectMode.UFO_CSV)
            {
                Windows.Storage.StorageFile file = (Windows.Storage.StorageFile)listBox.SelectedItem;
                TB_UfoCsvPath.Text = file.Path.ToString();
                sUfoCsvFile = await GetCsvData(file);
                sUfoCsvRowIndex = 0;
                /*End*/
                DLG_FileSelect.Hide();
            }
            else
            {
                DLG_FileSelect.Hide();
            }
        }

        private void BT_FileSelectSkip_Click(object sender, RoutedEventArgs e)
        {
            if (sSelectMode == FileSelectMode.MEDIA_FILE)
            {
                sSelectMode = FileSelectMode.CYC_CSV;
                listBox.Items.Clear();
                DLG_FileSelect.Title = "サイクロンSA用のCSVファイルを選択してください";
                for (int i = 0; i < FileList.Count; i++)
                {
                    if (FileList[i].FileType == ".csv")
                    {
                        listBox.Items.Add(FileList[i]);
                    }
                }
            }
            else if (sSelectMode == FileSelectMode.CYC_CSV)
            {
                sSelectMode = FileSelectMode.UFO_CSV;
                DLG_FileSelect.Title = "UFO SA用のCSVファイルを選択してください";
            }
            else if (sSelectMode == FileSelectMode.UFO_CSV)
            {
                /*End*/
                DLG_FileSelect.Hide();
            }
            else
            {
                DLG_FileSelect.Hide();
            }
        }

        private void listBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            FileSelectSequence();
        }

        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null != listBox.SelectedItem)
            {
                BT_FileSelectOK.IsEnabled = true;
            }
            else {
                BT_FileSelectOK.IsEnabled = false;
            }
        }

        private async void CMB_PlayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectName = (string)CMB_PlayList.SelectedItem;
            if (selectName != "") {
                string mediaPath = "";
                string cycCsvPath = "";
                string ufoCsvPath = "";
                BT_DeletePlayList.IsEnabled = true;
                /*ファイル読み込み処理*/

                var xElements = sPlayList.Elements("PlayInfo");
                foreach (XElement xElement in xElements)
                {
                    if (xElement.Element("Name").Value == selectName )
                    {
                        mediaPath = xElement.Element("MediaPath").Value;
                        cycCsvPath = xElement.Element("CycCsvPath").Value;
                        ufoCsvPath = xElement.Element("UfoCsvPath").Value;
                    }
                }
                TB_MediaPath.Text = mediaPath;
                TB_UfoCsvPath.Text = ufoCsvPath;
                TB_CycCsvPath.Text = cycCsvPath;


                /*Media*/
                if (mediaPath != "") {
                    //Windows.Storage.StorageFile mediaFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(mediaPath);
                    try
                    {
                        Windows.Storage.StorageFile mediaFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(mediaPath);
                        SetMediaFile(mediaFile);
                    }
                    catch {
                        MessageDialog messageDialog = new MessageDialog("メディアファイルが開けません。ファイルパスが正しいか、" +
                            "プライバシー設定のファイルシステム設定確認してください");
                        await messageDialog.ShowAsync();
                        return;
                    }

                }

                if (cycCsvPath != "")
                {
                    try
                    {
                        Windows.Storage.StorageFile cycCsvFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(cycCsvPath);
                        sCycCsvFile = await GetCsvData(cycCsvFile);
                        sCycCsvRowIndex = 0;
                    }
                    catch {
                        MessageDialog messageDialog = new MessageDialog("サイクロン用CSVファイルが開けません。ファイルパスが正しいか、" +
                            "プライバシー設定のファイルシステム設定確認してください");
                        await messageDialog.ShowAsync();
                        return;
                    }
                }

                if (ufoCsvPath != "")
                {
                    try {
                        Windows.Storage.StorageFile UfoCsvFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(ufoCsvPath);
                        sUfoCsvFile = await GetCsvData(UfoCsvFile);
                        sUfoCsvRowIndex = 0;
                    }
                    catch
                    {
                        MessageDialog messageDialog = new MessageDialog("UFO用CSVファイルが開けません。ファイルパスが正しいか、" +
                                "プライバシー設定のファイルシステム設定確認してください");
                        await messageDialog.ShowAsync();
                        return;
                    }
                }
                
            }
        }
        private async void BT_AddPlayList_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            if (TB_MediaPath.Text != "") {
                count++;
            }
            if (TB_CycCsvPath.Text != "")
            {
                count++;
            }
            if (TB_UfoCsvPath.Text != "")
            {
                count++;
            }

            if (count > 0) {
                TB_PlayInfoName.Text = "";
                var test = await DLG_AddPlayList.ShowAsync(); 
            }
        }

        private void DLG_AddPlayList_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (TB_PlayInfoName.Text != "")
            {
                XElement datas = new XElement("PlayInfo",
                    new XElement("Name", TB_PlayInfoName.Text),
                    new XElement("MediaPath", TB_MediaPath.Text),
                    new XElement("CycCsvPath", TB_CycCsvPath.Text),
                    new XElement("UfoCsvPath", TB_UfoCsvPath.Text)
                   );
                sPlayList.Add(datas);

                //追加した情報を保存する
                sPlayList.Save(sXmlFilePath);
                ReadPlayList();
            }

        }


        private void TB_PlayInfoName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TB_PlayInfoName.Text == "")
            {
                DLG_AddPlayList.IsPrimaryButtonEnabled = false;
            }
            else
            {
                DLG_AddPlayList.IsPrimaryButtonEnabled = true;
            }
        }

        private void BT_DeletePlayList_Click(object sender, RoutedEventArgs e)
        {
            string selectName = (string)CMB_PlayList.SelectedItem;
            if (selectName != "") {
                var xElements = sPlayList.Elements("PlayInfo");
                foreach (XElement xElement in xElements)
                {
                    if (xElement.Element("Name").Value == selectName)
                    {
                        xElement.Remove();
                    }
                }

                sPlayList.Save(sXmlFilePath);
                ReadPlayList();
            }
        }
    }
}
