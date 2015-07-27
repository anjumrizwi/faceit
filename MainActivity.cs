using System;
using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Provider;
using Microsoft.ProjectOxford.Face;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face.Contract;
using System.IO;
using Java.Text;

namespace FaceIt
{
    [Activity(Label = "FaceIt", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private const string FileType_Image = "image/*";
        private const string PromptText_SelectImage = "Select image";
        private const int PICK_IMAGE = 1;
        private const int TAKE_PICTURE = 2;
        private Android.Net.Uri TempFileUri;
        private const string FaceAPIKey = "YOUR_KEY";
        private readonly FaceServiceClient faceServiceClient = new FaceServiceClient(FaceAPIKey);
        private static Color StrokeColor = Color.LightGreen;
        private static int strokeWidth = 6;
        private static int imageQuality = 100;
        private static int textSize = 30;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.Main);

            Button browseButton = FindViewById<Button>(Resource.Id.ButtonBrowse);

            //When "Browse" button clicked, fire an intent to select image
            browseButton.Click += delegate
            {
                Intent imageIntent = new Intent();
                imageIntent.SetType(FileType_Image);
                imageIntent.SetAction(Intent.ActionGetContent);
                StartActivityForResult(
                    Intent.CreateChooser(imageIntent, PromptText_SelectImage), PICK_IMAGE);
            };

            Button takePictureButton = FindViewById<Button>(Resource.Id.ButtonTakePicture);

            //When "Take a picture" button clicked, fire an intent to take picture
            takePictureButton.Click += delegate
            {
                Intent takePictureIntent = new Intent(MediaStore.ActionImageCapture);
                if (takePictureIntent.ResolveActivity(PackageManager) != null)
                {
                    // Create the File where the photo should go
                    Java.IO.File photoFile = null;
                    try
                    {
                        photoFile = createImageFile();
                    }
                    catch (Java.IO.IOException)
                    {
                        //TODO: Error handling
                    }  
                    // Continue only if the File was successfully created
                    if (photoFile != null)
                    {
                        takePictureIntent.PutExtra(MediaStore.ExtraOutput, 
                            Android.Net.Uri.FromFile(photoFile));

                        //Delete this temp file, only keey its Uri information
                        photoFile.Delete();

                        TempFileUri = Android.Net.Uri.FromFile(photoFile);
                        StartActivityForResult(takePictureIntent, TAKE_PICTURE);
                    }
                }
            };
                    
        }

        protected async override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == PICK_IMAGE && resultCode == Result.Ok)
            {
                try
                {
                    Bitmap bitmap = BitmapFactory.DecodeStream(
                            ContentResolver.OpenInputStream(data.Data)
                        );
                    Bitmap processedBitmap = await detectFacesAndMarkThem(bitmap);
                    var imageView =
                    FindViewById<ImageView>(Resource.Id.bitmapImageView);
                    imageView.SetImageBitmap(processedBitmap);
                }
                catch (Exception)
                {
                    //TODO: error handling
                }
            }
            else if (requestCode == TAKE_PICTURE && resultCode == Result.Ok)
            {
                try
                {
                    Bitmap bitmap = MediaStore.Images.Media.GetBitmap(
                            ContentResolver, TempFileUri
                        );
                    Bitmap processedBitmap = await detectFacesAndMarkThem(bitmap);
                    var imageView =
                    FindViewById<ImageView>(Resource.Id.bitmapImageView);
                    imageView.SetImageBitmap(processedBitmap);
                }
                catch (Exception)
                {
                    //TODO: error handling
                }
            }
        }

        /// <summary>
        /// Creates a temp image file in order to obtain a valid Uri.
        /// </summary>
        /// <returns></returns>
        private Java.IO.File createImageFile()
        {
            // Create an image file name
            string timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss").Format(new Java.Util.Date());
            string imageFileName = "JPEG_" + timeStamp + "_";
            Java.IO.File storageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(
            Android.OS.Environment.DirectoryPictures);

            if (!storageDir.Exists())
            {
                storageDir.Mkdir();
            }

            Java.IO.File image = Java.IO.File.CreateTempFile(
                    imageFileName,  /* prefix */
                    ".jpg",         /* suffix */
                    storageDir      /* directory */
                );
            
            return image;
        }

        /// <summary>
        /// Detect faces from a bitmap, and directly mark information on this bitmao
        /// </summary>
        /// <param name="originalBitmap"></param>
        /// <returns></returns>
        private static async Task<Bitmap> detectFacesAndMarkThem(Bitmap originalBitmap)
        {
            FaceServiceClient client = new FaceServiceClient(FaceAPIKey);
            MemoryStream stream = new MemoryStream();
            originalBitmap.Compress(Bitmap.CompressFormat.Jpeg, imageQuality, stream);

            Face[] faces = await client.DetectAsync(stream.ToArray());

            Bitmap resultBitmap = drawFaceRectanglesOnBitmap(originalBitmap, faces);

            return resultBitmap;
        }
        
        /// <summary>
        /// Mark bitmap with given face information
        /// </summary>
        /// <param name="originalBitmap"></param>
        /// <param name="faces"></param>
        /// <returns></returns>
        private static Bitmap drawFaceRectanglesOnBitmap(Bitmap originalBitmap, Face[] faces)
        {
            Bitmap bitmap = originalBitmap.Copy(Bitmap.Config.Argb8888, true);
            Canvas canvas = new Canvas(bitmap);
            Paint paint = new Paint();
            paint.Color = StrokeColor;
            paint.StrokeWidth = strokeWidth;
            paint.TextSize = textSize;
            if (faces != null)
            {
                foreach(Face face in faces)
                {
                    FaceRectangle faceRectangle = face.FaceRectangle;

                    paint.SetStyle(Paint.Style.Stroke);
                    canvas.DrawRect(
                            faceRectangle.Left,
                            faceRectangle.Top,
                            faceRectangle.Left + faceRectangle.Width,
                            faceRectangle.Top + faceRectangle.Height,
                            paint);

                    paint.SetStyle(Paint.Style.Fill);
                    canvas.DrawText(face.Attributes.Gender + ", " + 
                        face.Attributes.Age + " y/o", 
                        faceRectangle.Left, faceRectangle.Top - textSize, paint);
                }
            }
            return bitmap;
        }
    }
}