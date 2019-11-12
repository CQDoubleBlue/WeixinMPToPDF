using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace WeixinMPToPDF.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            ////if (IsInDesignMode)
            ////{
            ////    // Code runs in Blend --> create design time data.
            ////}
            ////else
            ////{
            ////    // Code runs "for real"
            ////}
            DownloadImageCommand = new RelayCommand(DownloadImage);
            MakePdfCommand = new RelayCommand(MakePdf);
            Status = "准备就绪";
            WebClient = new WebClient();
            WebClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.87 Safari/537.36");
        }

        public RelayCommand DownloadImageCommand { get; set; }
        public RelayCommand MakePdfCommand { get; set; }

        private WebClient WebClient { get; }

        private string url = "https://mp.weixin.qq.com/s/eClyUOaV2-YfMuyqDv36QQ";

        public string Url
        {
            get { return url; }
            set { url = value;RaisePropertyChanged(); }
        }

        private string status;

        public string Status
        {
            get { return status; }
            set { status = value; RaisePropertyChanged(); }
        }


        public void DownloadImage()
        {
            if (string.IsNullOrEmpty(Url))
            {
                MessageBox.Show("请输入url");
                return;
            }

            new Thread(() =>
            {
                var html = WebClient.DownloadString(new Uri(Url));
                var regex = new Regex("data-src=\"(.*?)\"");
                var matches = regex.Matches(html);
                if (matches.Count == 0)
                {
                    MessageBox.Show("此url没有图片");
                }

                Status = $"检测到{matches.Count}张图片";
                if(Directory.Exists("temp"))
                    Directory.Delete("temp", true);
                Thread.Sleep(10);
                Directory.CreateDirectory("temp");
                var index = 1;
                foreach (Match match in matches)
                {
                    Status = $"图片下载{index}/{matches.Count}";
                    var imageUrl = match.Groups[1].Value;
                    var ext = imageUrl.Substring(imageUrl.Length - 3);
                    WebClient.DownloadFile(match.Groups[1].Value, $"temp/{(index++):D3}.{ext}");
                }

                Status = "图片下载完成";
                ProcessImage();
            }).Start();

        }

        private void ProcessImage()
        {
            Directory.CreateDirectory("temp/processed");
            var files = Directory.GetFiles("temp");
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, file =>
            {
                if (Directory.Exists(file)) return;
                Status = $"正在处理{file}";
                var ext = Path.GetExtension(file);
                var image = new Bitmap(file);
                var index = 1;
                var cutPos = 0;
                var minCutHeight = 60;
                for (int i = minCutHeight; i < image.Height;)
                {
                    var lineColors = new List<Color>();
                    for (int j = 0; j < image.Width; j++)
                    {
                        var color = image.GetPixel(j, i);
                        if (!lineColors.Contains(color)) lineColors.Add(color);
                    }

                    if (lineColors.Count <= 2)
                    {
                        var newImage = image.Clone(new Rectangle(0, cutPos, image.Width, i - cutPos),
                            image.PixelFormat);
                        newImage.Save(file.Replace(ext, $"_{index++:D3}{ext}").Replace("\\", "/")
                            .Replace("temp/", "temp/processed/"));
                        cutPos = i;
                        i += minCutHeight;
                    }
                    else
                    {
                        i++;
                    }
                }
            });
            Status = "图片处理完成";
            MessageBox.Show(Status);
        }

        public void MakePdf()
        {
            var files = Directory.GetFiles("temp/processed");
            if (files.Length == 0)
            {
                MessageBox.Show("还没有下载图片");
            }
            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            var margin = 10;
            double drawPos = margin;
            foreach (var file in files)
            {
                var image = XImage.FromFile(file);
                double rate = 1;
                if (image.PixelWidth > page.Width.Value - margin * 2)
                {
                    rate = (page.Width.Value - margin * 2) / image.PixelWidth;
                }

                if (image.PixelHeight > page.Height.Value - margin * 2)
                {
                    rate = (page.Height.Value - margin * 2) / image.PixelHeight;
                }
                var realWidth = image.PixelWidth * rate;
                var realHeight = image.PixelHeight * rate;
                if (drawPos + realHeight > page.Height)
                {
                    page = pdf.AddPage();
                    drawPos = margin;
                }
                using (var gfx = XGraphics.FromPdfPage(page))
                {
                    gfx.DrawImage(image, (page.Width.Value - realWidth) / 2 - margin, drawPos, realWidth, realHeight);
                    drawPos += realHeight;
                }
            }
            pdf.Save($"out-{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }

    }
}