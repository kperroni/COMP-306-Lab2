using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using _300825160_Perroni__300930438__Lemos__Lab2.Models;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using System.IO;
using Amazon.S3.Model;

namespace _300825160_Perroni__300930438__Lemos__Lab2.Controllers
{
    public class MoviesController : Controller
    {
        private const string bucketName = "bucket4kenny";
        private const string destName = "s3file.txt";
        private const string sourcePath = "C:\\Users\\T410\\Centennial College\\Semester IV\\API & Cloud Computing\\Week 2\\testFile.txt";
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.USEast2;
        //SignInManager<IdentityUser> SignInManager;
        static IAmazonS3 s3 { get; set; }

        private readonly kenny_andre_lab2Context _context;

        public MoviesController(kenny_andre_lab2Context context, IAmazonS3 s3Client)
        {
            _context = context;
            s3 = s3Client;
        }

        // GET: Movies
        public async Task<IActionResult> Index()
        {
           // if (SignInManager.IsSignedIn(User))
           // {
                return View(await _context.Movie.ToListAsync());
           // }
           // return View("~/Views/Home/Index.cshtml");
        }

        // GET: Movies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie
                .FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }

        // GET: Movies/Create
        public IActionResult Create()
        {
            // This code downloads a movie given a title
            // The title must be accurate since it is how it was stored in the S3 bucket
            // **This code works; however, the call should be done through a button**
           // GetMovieAsync("a movie").Wait();
            return View();
        }

        // POST: Movies/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Sinopsis,Genre,ReleaseDate,Duration")] Movie movie, Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (ModelState.IsValid)
            {
                if (file == null || file.Length == 0)
                {
                    Debug.WriteLine("No file selected");
                }
                else
                {
                    Debug.WriteLine("File was selected");
                }

                // Push movie to S3 bucket
                UploadMovieAsync(movie.Title, file).Wait();
                // After pushing, populate the URL property of the movie and then insert in the db

                _context.Add(movie);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        private static async Task UploadMovieAsync(string MovieTitle, Microsoft.AspNetCore.Http.IFormFile file)
        {
            // get the file and convert it to the byte[]
            byte[] fileBytes = new Byte[file.Length];
            file.OpenReadStream().Read(fileBytes, 0, Int32.Parse(file.Length.ToString()));

            // create unique file name for prevent the mess
            //var fileName = Guid.NewGuid() + file.FileName;

            PutObjectResponse response = null;

            using (var stream = new MemoryStream(fileBytes))
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = MovieTitle,
                    InputStream = stream,
                    ContentType = file.ContentType,
                    CannedACL = S3CannedACL.PublicRead
                };

                response = await s3.PutObjectAsync(request);
            };

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                Debug.WriteLine("File uploaded" );
            }
            }

        public async Task<IActionResult> GetMovieAsync(int? id)
        {
            var movie = await _context.Movie
                   .FirstOrDefaultAsync(m => m.Id == id);
            string FileName = movie.Title;
            try
            {
                Debug.WriteLine("In the try creating instance to download");
                var fileTransferUtility = new TransferUtility(s3);
                // Option 1
                // await fileTransferUtility.UploadAsync(sourcePath, bucketName);
                // Debug.WriteLine("Upload 1 Complete");

                Debug.WriteLine(FileName);
                // Option 2
                Debug.WriteLine("Before download");
                await fileTransferUtility.DownloadAsync("C:\\Users\\"+Environment.ExpandEnvironmentVariables("%USERNAME%")+"\\Downloads\\" + FileName, bucketName, FileName);
                Debug.WriteLine("Download 2 Complete");
            }
            catch (AmazonS3Exception e)
            {
                Debug.WriteLine("Error encountered in the server" + e.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unknown error encountered in the server." + e.Message);
            }
            ViewData["DownloadComplete"] = "Download complete!";
            return View("Index", await _context.Movie.ToListAsync());
        }

        // GET: Movies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.FindAsync(id);
            if (movie == null)
            {
                return NotFound();
            }
            return View(movie);
        }

        // POST: Movies/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Sinopsis,Genre,ReleaseDate,Duration")] Movie movie)
        {
            if (id != movie.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        // GET: Movies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie
                .FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }

        // POST: Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movie.FindAsync(id);
            _context.Movie.Remove(movie);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int id)
        {
            return _context.Movie.Any(e => e.Id == id);
        }
    }
}
