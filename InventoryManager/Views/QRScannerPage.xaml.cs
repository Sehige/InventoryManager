using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;
using InventoryManager.Services;
using InventoryManager.Models;

namespace InventoryManager.Views
{
    public partial class QRScannerPage : ContentPage
    {
        private readonly InventoryService _inventoryService;
        private readonly AuthService _authService;
        private bool _isProcessing = false;
        private bool _isTorchOn = false;

        public QRScannerPage(InventoryService inventoryService, AuthService authService)
        {
            InitializeComponent();
            _inventoryService = inventoryService;
            _authService = authService;

            // Configure camera options in code
            cameraView.Options = new ZXing.Net.Maui.BarcodeReaderOptions
            {
                Formats = ZXing.Net.Maui.BarcodeFormat.QrCode,
                AutoRotate = true,
                Multiple = false,
                TryHarder = true,
                TryInverted = true
            };

            // Start scanning line animation
            StartScanLineAnimation();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            cameraView.IsDetecting = true;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            cameraView.IsDetecting = false;
        }

        private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
        {
            // Prevent multiple simultaneous processing
            if (_isProcessing)
                return;

            _isProcessing = true;

            try
            {
                // Stop detection temporarily
                cameraView.IsDetecting = false;

                // Get the first QR code
                var barcode = e.Results?.FirstOrDefault();
                if (barcode == null)
                {
                    _isProcessing = false;
                    cameraView.IsDetecting = true;
                    return;
                }

                // Haptic feedback
                HapticFeedback.Perform(HapticFeedbackType.Click);

                // Show loading overlay
                await MainThread.InvokeOnMainThreadAsync(() => loadingOverlay.IsVisible = true);

                // Parse QR data
                var qrData = InventoryQRData.FromQRString(barcode.Value);

                if (qrData == null)
                {
                    await DisplayAlert("Invalid QR Code",
                        "This QR code does not contain valid inventory information.",
                        "Try Again");

                    _isProcessing = false;
                    loadingOverlay.IsVisible = false;
                    cameraView.IsDetecting = true;
                    return;
                }

                // Fetch inventory item details from database
                var item = await _inventoryService.GetItemByCodeAsync(qrData.ItemCode);

                if (item == null)
                {
                    await DisplayAlert("Item Not Found",
                        $"No item found with code: {qrData.ItemCode}",
                        "OK");

                    _isProcessing = false;
                    loadingOverlay.IsVisible = false;
                    cameraView.IsDetecting = true;
                    return;
                }

                // Navigate to item details page
                var itemDetailsPage = new ItemDetailsPage(_inventoryService, item, DateTime.Now);
                await Navigation.PushAsync(itemDetailsPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"An error occurred while processing the QR code: {ex.Message}",
                    "OK");

                _isProcessing = false;
                loadingOverlay.IsVisible = false;
                cameraView.IsDetecting = true;
            }
        }

        private void OnTorchClicked(object sender, EventArgs e)
        {
            _isTorchOn = !_isTorchOn;
            cameraView.IsTorchOn = _isTorchOn;

            // Update button appearance
            torchButton.BackgroundColor = _isTorchOn ? Colors.Orange : Colors.DarkGray;
        }

        private async void OnManualInputClicked(object sender, EventArgs e)
        {
            // Show manual input dialog
            string result = await DisplayPromptAsync(
                "Manual Entry",
                "Enter Item Code:",
                placeholder: "ITEM-12345",
                keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(result))
            {
                // Create QR data from manual input
                var qrData = new InventoryQRData
                {
                    ItemCode = result.Trim(),
                    ManufactureDate = DateTime.Now,
                    BatchNumber = "MANUAL",
                    Location = WarehouseLocation.MainWarehouse
                };

                // Process as if it was scanned
                await ProcessManualEntry(qrData);
            }
        }

        private async Task ProcessManualEntry(InventoryQRData qrData)
        {
            try
            {
                loadingOverlay.IsVisible = true;

                var item = await _inventoryService.GetItemByCodeAsync(qrData.ItemCode);

                if (item == null)
                {
                    await DisplayAlert("Item Not Found",
                        $"No item found with code: {qrData.ItemCode}",
                        "OK");
                    return;
                }

                var itemDetailsPage = new ItemDetailsPage(_inventoryService, item, DateTime.Now);
                await Navigation.PushAsync(itemDetailsPage);
            }
            finally
            {
                loadingOverlay.IsVisible = false;
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private void StartScanLineAnimation()
        {
            var animation = new Animation(v => scanLine.TranslationY = v, 0, 240);
            animation.Commit(this, "ScanLineAnimation", 16, 2000, Easing.Linear,
                (v, c) => scanLine.TranslationY = 0, () => true);
        }
    }
}