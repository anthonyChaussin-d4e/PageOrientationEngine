using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PageOrientationEngine.Helpers
{
    /// <summary>
    /// This class contains several methods to work with Tiff files
    /// </summary>
    public class TiffUtils
    {
        #region GetPageCount
        /// <summary>
        /// Returns the number of pages in the <paramref name="inputFile"/>
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        public static int GetPageCount(string inputFile)
        {
            return GetPageCount(Image.FromFile(inputFile), true);
        }

        /// <summary>
        /// Returns the number of pages in the <paramref name="image"/>
        /// </summary>
        /// <param name="image">The image</param>
        /// <param name="dispose">Set to true to dispose the image after returning the number of pages</param>
        /// <returns></returns>
        private static int GetPageCount(Image image, bool dispose)
        {
            int pages = image.GetFrameCount(new FrameDimension(image.FrameDimensionsList[0]));

            if (dispose)
                image.Dispose();

            return pages;
        }
        #endregion

        #region SplitTiffImage
        /// <summary>
        /// Splits the multipage tiff <paramref name="inputFile"/> to the <paramref name="outputFolder"/>
        /// and returns a list of string to the splitted files
        /// </summary>
        /// <param name="inputFile">The tiff file to split</param>
        /// <param name="outputFolder">The folder where to write the splitted files</param>
        /// <returns></returns>
        public static List<string> SplitTiffImage(string inputFile, string outputFolder)
        {
            using (var image = Image.FromFile(inputFile))
            {

                List<string> output = new List<string>();

                for (var i = 0; i < GetPageCount(image, false); i++)
                {
                    image.SelectActiveFrame(new FrameDimension(image.FrameDimensionsList[0]), i);
                    //Save the master bitmap
                    var fileName = string.Format("{0}_pagina{1}.tif", outputFolder + Path.DirectorySeparatorChar +
                                 Path.GetFileNameWithoutExtension(inputFile) + "_", i + 1);
                    image.Save(fileName, GetEncoderInfo("image/tiff"), new EncoderParameters(1)
                    {
                        Param = new[]
                    {
                        new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4)
                    }
                    });
                    output.Add(fileName);
                }

                return output;
            }
        }

        /// <summary>
        /// Splits the multipage tiff <paramref name="inputFile"/> to a list of memory streams
        /// and returns a list of string to the splitted files
        /// </summary>
        /// <param name="inputFile">The tiff file to split</param>
        /// <returns></returns>
        public static List<MemoryStream> SplitTiffImage(string inputFile)
        {
            using (Image image = Image.FromFile(inputFile))
            {
                return SplitToStream(image);
            }
        }
        private static List<MemoryStream> SplitToStream(Image image)
        {
            List<MemoryStream> output = new List<MemoryStream>();

            for (var i = 0; i < GetPageCount(image, false); i++)
            {
                image.SelectActiveFrame(new FrameDimension(image.FrameDimensionsList[0]), i);
                var memoryStream = new MemoryStream();
                image.Save(memoryStream, GetEncoderInfo("image/tiff"), new EncoderParameters(1) { Param = new[] { new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4) } });
                output.Add(memoryStream);
            }

            return output;

        }

        /// <summary>
        /// Splits the multipage tiff <paramref name="inputFile"/> to a list of memory streams
        /// and returns a list of string to the splitted files
        /// </summary>
        /// <param name="inputFile">The tiff file to split</param>
        /// <returns></returns>
        public static List<MemoryStream> SplitTiffImage(MemoryStream inputFile)
        {
            using (var image = Image.FromStream(inputFile))
            {
                return SplitToStream(image);
            }
        }
        #endregion

        #region ConcatTiffImages
        /// <summary>
        /// Concatenes the <paramref name="inputFiles"/> to one Tiff file and saves it to the <paramref name="outputFile"/>
        /// </summary>
        /// <param name="inputFiles">A list with tiff files to concatenate</param>
        /// <param name="outputFile">The output file</param>
        /// <returns></returns>
        public static void ConcatenateTiffImages(List<string> inputFiles, string outputFile)
        {
            var saveFlag = Encoder.SaveFlag;
            var encoderParameters = new EncoderParameters(2)
            {
                Param = new[]
                {
                     new EncoderParameter(saveFlag, (long)EncoderValue.MultiFrame),
                     new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4)
                }
            };

            Image firstImage = null;

            inputFiles.ForEach(inputFile =>
            {
                if (firstImage == null)
                {
                    firstImage = Image.FromFile(inputFile);
                    firstImage.Save(outputFile, GetEncoderInfo("image/tiff"), encoderParameters);
                }
                else
                {
                    encoderParameters.Param[0] = new EncoderParameter(saveFlag, (long)EncoderValue.FrameDimensionPage);

                    var image = Image.FromFile(inputFile);
                    firstImage.SaveAdd(image, encoderParameters);
                    image.Dispose();
                }
            });

            if (firstImage != null)
                firstImage.Dispose();
        }

        /// <summary>
        /// Concatenes the <paramref name="inputFiles"/> to one Tiff file and returns it as a memory stream
        /// </summary>
        /// <param name="inputFiles">A list with tiff files to concatenate</param>
        /// <returns></returns>
        public static MemoryStream ConcatenateTiffImages(List<string> inputFiles)
        {
            var saveFlag = Encoder.SaveFlag;
            var encoderParameters = new EncoderParameters(2)
            {
                Param = new[]
                {
                     new EncoderParameter(saveFlag, (long)EncoderValue.MultiFrame),
                     new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4)
                }
            };
            Image firstImage = null;
            var memoryStream = new MemoryStream();

            inputFiles.ForEach(inputFile =>
            {
                if (firstImage == null)
                {
                    firstImage = Image.FromFile(inputFile);
                    firstImage.Save(memoryStream, GetEncoderInfo("image/tiff"), encoderParameters);
                }
                else
                {
                    encoderParameters.Param[0] = new EncoderParameter(saveFlag, (long)EncoderValue.FrameDimensionPage);

                    var image = Image.FromFile(inputFile);
                    firstImage.SaveAdd(image, encoderParameters);
                    image.Dispose();
                }
            });

            if (firstImage != null)
                firstImage.Dispose();

            return memoryStream;
        }
        #endregion

        #region ChangeTiffResolution
        /// <summary>
        /// Sets the resolution of the given <paramref name="inputFile"/> to the new give <paramref name="resolution"/>
        /// and saves the new file to the <paramref name="outputFile"/>
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputFile"></param>
        /// <param name="resolution"></param>
        public static void ChangeTiffResolution(string inputFile, string outputFile, int resolution)
        {
            var saveFlag = Encoder.SaveFlag;
            var encoderParameters = new EncoderParameters(2)
            {
                Param = new[]
                {
                     new EncoderParameter(saveFlag, (long)EncoderValue.MultiFrame),
                     new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4)
                }
            };
            Bitmap outputBitmap = null;

            SplitTiffImage(inputFile).ForEach(memoryStream =>
            {
                if (outputBitmap == null)
                {
                    outputBitmap = (Bitmap)Image.FromStream(memoryStream);
                    outputBitmap.SetResolution(resolution, resolution);
                    outputBitmap.Save(outputFile, GetEncoderInfo("image/tiff"), encoderParameters);
                }
                else
                {
                    encoderParameters.Param[0] = new EncoderParameter(saveFlag, (long)EncoderValue.FrameDimensionPage);
                    var bitMap = (Bitmap)Image.FromStream(memoryStream);
                    bitMap.SetResolution(resolution, resolution);
                    outputBitmap.SaveAdd(bitMap, encoderParameters);
                    bitMap.Dispose();
                }
            });

            if (outputBitmap != null)
                outputBitmap.Dispose();
        }
        #endregion

        #region GetEncodeInfo
        /// <summary>
        /// Returns a list with available image codecs for the given <paramref name="mimeType"/>
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            foreach (var encoder in ImageCodecInfo.GetImageEncoders())
            {
                if (encoder.MimeType == mimeType)
                    return encoder;
            }

            throw new Exception(mimeType + " mime type not found in ImageCodecInfo");
        }
        #endregion
    }
}