using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;

using PageOrientationEngine.Helpers;

using Tesseract;

namespace PageOrientationEngine
{
    public class DocumentInspector
    {
        #region Properties
        /// <summary>
        /// The path to the Tesseract data files
        /// </summary>
        public string TesseractDataPath { get; private set; }

        /// <summary>
        /// The language that needs to be used by Tesseract
        /// </summary>
        public string TesseractLanguage { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates this object
        /// </summary>
        /// <param name="dataPath">The path to the Tesseract language files</param>
        /// <param name="language">The Tesseract languag to use, e.g. eng</param>
        public DocumentInspector(string dataPath, string language)
        {
            TesseractDataPath = dataPath;
            TesseractLanguage = language;
        }
        #endregion

        #region DetectPageOrientation
        /// <summary>
        /// Returns a list with <see cref="DocumentInspectorPageOrientation">DocumentInspectorPageOrientations</see>
        /// according to the amount of <paramref name="memoryStreams"/>
        /// </summary>
        /// <param name="memoryStreams"></param>
        /// <returns></returns>
        public List<Tuple<int, Orientation, Image>> DetectPageOrientation(List<MemoryStream> memoryStreams)
        {
            List<Task> taskList = new List<Task>();
            List<Tuple<int, Orientation, Image>> result = new List<Tuple<int, Orientation, Image>>();
            // WorkQueue vullen
            var i = 1;
            memoryStreams.ForEach(m =>
            {
                taskList.Add(Task.Run(() => result.Add(Tuple.Create( i, this.DetectPageOrientation((Bitmap)Image.FromStream(memoryStreams[i - 1])), Image.FromStream(memoryStreams[i - 1])))));
                i++;
            });
            Task.WaitAll(taskList.ToArray());

            return result;
        }

        /// <summary>
        /// Returns a list with <see cref="DocumentInspectorPageOrientation">DocumentInspectorPageOrientations</see>
        /// according to the amount of "pages" in the <paramref name="inputFile"/>
        /// </summary>
        /// <param name="inputFile">The input file</param>
        /// <returns></returns>
        public List<Tuple<int, Orientation, Image>> DetectPageOrientation(string inputFile)
        {
            string extention = Path.GetExtension(inputFile);
            if (extention == ".tiff")
            {
                var result = DetectPageOrientation(TiffUtils.SplitTiffImage(inputFile));
                result.Sort((t, t2) => t.Item1.CompareTo(t2.Item1));
                return result;
            }
            else if (extention == ".pdf")
            {
                var _rasterizer = new GhostscriptRasterizer();
                List<Tuple<int, Orientation, Image>> result = new List<Tuple<int, Orientation, Image>>();
                List<Task> tasks = new List<Task>();

                using (FileStream fs = File.Open(inputFile, FileMode.Open, FileAccess.Read))
                {
                    _rasterizer.Open(fs, new GhostscriptVersionInfo(new Version(0, 0, 0),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gsdll32.dll"),
                    string.Empty, GhostscriptLicense.GPL), false);
                }
                for (int p = 1; p <= _rasterizer.PageCount; p++)
                {
                    Image img = _rasterizer.GetPage(300, p);
                    tasks.Add(Task.Run(() => result.Add(Tuple.Create(p-1, DetectPageOrientation((Bitmap)img), img))));
                }
                _rasterizer.Close();
                _rasterizer.Dispose();
                Task.WaitAll(tasks.ToArray());
                result.Sort((t, t2) => t.Item1.CompareTo(t2.Item1));
                return result;
            }
            else
            {
                throw new ArgumentException("This file format is not supported yet.", nameof(inputFile));
            }
            
        }

        /// <summary>
        /// Returns the <see cref="Orientation"/> of the <paramref name="bitmap"/> 
        /// according to the text that is on it
        /// </summary>
        /// <param name="bitmap">The bitmap with text</param>
        /// <returns></returns>
        public Orientation DetectPageOrientation(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new NullReferenceException("The bitmap parameter is not set");

            if (bitmap.PixelFormat == PixelFormat.Format1bppIndexed)
                bitmap = BitmapUtils.CopyToBpp(bitmap, 1);

            using (var engine = new TesseractEngine(TesseractDataPath, TesseractLanguage))
            using (var image = PixConverter.ToPix(bitmap))
            using (var page = engine.Process(image, PageSegMode.AutoOsd))
                return page.AnalyseLayout().GetProperties().Orientation;
        }
        #endregion
    }
}
