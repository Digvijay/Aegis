
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Aegis.Integrity.Discovery;
using Aegis.Integrity.Protocol;

// Requires Microsoft.WindowsAppSDK reference.
// Implements standard WinUI 3 windowing and canvas logic.

namespace Aegis.Visualizer
{
    public sealed partial class MainWindow : Window
    {
        private GridLawDetector _detector = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<GridLawDetector>.Instance);

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".pdf");
            
            // Get the Window Handle (HWND) for WinUI 3 Interop
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            picker.InitializeWithWindow(hwnd);
            
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                StatusText.Text = $"Processing {file.Name}...";
                await ProcessPdf(file);
                StatusText.Text = "Ready.";
            }
        }

        private async Task ProcessPdf(Windows.Storage.StorageFile file)
        {
            using var stream = await file.OpenStreamForReadAsync();
            using var aegisDoc = UglyToad.PdfPig.PdfDocument.Open(stream);

            // Clear previous drawings
            MainCanvas.Children.Clear();

            // Process Page 1 for visualization verification.
            if (aegisDoc.NumberOfPages > 0)
            {
                var pigPage = aegisDoc.GetPage(1);
                
                // Aegis Logic: Get Atoms
                var atoms = pigPage.GetWords().Select((w, idx) => new GeometricAtom(
                    w.Text,
                    new BoundingBox(w.BoundingBox.Left, w.BoundingBox.Bottom, w.BoundingBox.Width, w.BoundingBox.Height),
                    1,
                    1
                ) { Index = idx }).ToList();

                // Aegis Logic: Detect Tables
                var zones = _detector.DetectTableZones(atoms);

                // Draw Zones (Blue Boxes)
                foreach (var zon in zones)
                {
                    var zoneAtoms = atoms.Skip(zon.Start).Take(zon.End - zon.Start + 1);
                    
                    double minX = zoneAtoms.Min(a => a.Bounds.X);
                    double minY = zoneAtoms.Min(a => a.Bounds.Y);
                    double maxX = zoneAtoms.Max(a => a.Bounds.X + a.Bounds.Width);
                    double maxY = zoneAtoms.Max(a => a.Bounds.Y + a.Bounds.Height);
                    
                    // Convert PDF coordinates (Bottom-Left origin) to UI coordinates (Top-Left origin)
                    // Visualizer Scale Factor (Zoom) - adjusting for typical screen DPI vs PDF Point
                    double scale = 1.0; 
                    double uiY = (pigPage.Height - maxY) * scale;
                    double uiX = minX * scale;
                    
                    var rect = new Rectangle
                    {
                        Width = (maxX - minX) * scale,
                        Height = (maxY - minY) * scale,
                        Stroke = new SolidColorBrush(Microsoft.UI.Colors.Blue),
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                    };
                    
                    Canvas.SetLeft(rect, uiX);
                    Canvas.SetTop(rect, uiY);
                    
                    MainCanvas.Children.Add(rect);
                }
            }
        }
    }
}
