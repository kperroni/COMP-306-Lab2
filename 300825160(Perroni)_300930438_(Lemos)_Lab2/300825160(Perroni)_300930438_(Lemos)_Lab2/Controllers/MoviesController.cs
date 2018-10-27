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
using Microsoft.AspNetCore.Authorization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using _300825160_Perroni__300930438__Lemos__Lab2.DynamoDB;
using Amazon.DynamoDBv2.DocumentModel;

namespace _300825160_Perroni__300930438__Lemos__Lab2.Controllers
{
    [Authorize]
    public class MoviesController : Controller
    {
        private const string bucketName = "bucket4kenny";
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.USEast2;
        private UserManager<IdentityUser> _userManager;
        static IAmazonS3 s3 { get; set; }
        static IAmazonDynamoDB dynamoDb { get; set; }

        private readonly kenny_andre_lab2Context _context;

        public MoviesController(kenny_andre_lab2Context context, IAmazonS3 s3Client, UserManager<IdentityUser> userManager, IAmazonDynamoDB dynamoClient)
        {
            _context = context;
            s3 = s3Client;
            _userManager = userManager;
            dynamoDb = dynamoClient;
        }

        // GET: Movies
        public async Task<IActionResult> Index()
        {
            //await PushComment();
            //await ReadComments();
            return View(await  _context.Movie.Include(x => x.UserMovie).ToListAsync());
            //return View(await _context.Movie.ToListAsync());
        }

        // GET: Movies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.Include(x => x.UserMovie)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }
            ViewData["movieComments"] = await ReadCommentsOfMovieAsync(movie.Id);
            return View(movie);
        }

        // This function pushes a comment to the DynamoDB table in AWS
        // There must be a validation block to check if the movie has any comments registered
        // If not, a new item is created, otherwise, the movie-comment object must be loaded
        // and the list of comments must be updated
        [HttpPost]
        public async Task<IActionResult> PushComment(int movieId, string movieComment)
        {
            var context = new DynamoDBContext(dynamoDb);
            Comments item = await ReadCommentsOfMovieAsync(movieId);
            if (item != null)
            {
                UserComment uc = new UserComment
                {
                    Id = System.Guid.NewGuid().ToString(),
                    userId = _userManager.GetUserId(HttpContext.User),
                    comment = movieComment
                };
                item.userComment.Add(uc);
                await context.SaveAsync(item);
            }
            else
            {
                var comment = new Comments
                {
                    movieId = movieId,
                    userComment = new List<UserComment>
                 {
                     new UserComment
                     {
                         Id = System.Guid.NewGuid().ToString(),
                         userId = _userManager.GetUserId(HttpContext.User),
                         comment = movieComment
                     }
                 }
                };
                await context.SaveAsync(comment);
            }
            var movie = await _context.Movie.Include(x => x.UserMovie)
                 .FirstOrDefaultAsync(m => m.Id == movieId);
            ViewData["movieComments"] = await ReadCommentsOfMovieAsync(movie.Id);
            return View("Details", movie);
        }
        [HttpPost]
        public async Task<IActionResult> EditAComment(int movieId, string commentId, string userMovieComment)
        {
            var context = new DynamoDBContext(dynamoDb);
            Comments item = await ReadCommentsOfMovieAsync(movieId);

            item.userComment.Find(uc => uc.Id == commentId).comment = userMovieComment;
            await context.SaveAsync(item);
            var movie = await _context.Movie.Include(x => x.UserMovie)
                 .FirstOrDefaultAsync(m => m.Id == movieId);
            ViewData["movieComments"] = await ReadCommentsOfMovieAsync(movie.Id);
            return View("Details", movie);
        }
        [HttpPost]
        public async Task<IActionResult> DeleteAComment(int movieId, string commentId)
        {
            var context = new DynamoDBContext(dynamoDb);
            Comments item = await ReadCommentsOfMovieAsync(movieId);
            UserComment ucToDelete = item.userComment.Find(uc => uc.Id == commentId);
            item.userComment.Remove(ucToDelete);
            await context.SaveAsync(item);
            var movie = await _context.Movie.Include(x => x.UserMovie)
                 .FirstOrDefaultAsync(m => m.Id == movieId);
            ViewData["movieComments"] = await ReadCommentsOfMovieAsync(movie.Id);
            return View("Details", movie);
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
                
                // Populating object to store which user created what movie
                var userM = new UserMovie
                {
                    // This line gets the ID of the logged user
                    UserId = _userManager.GetUserId(HttpContext.User),
                    MovieId = movie.Id
                };
                movie.UserMovie.Add(userM);
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
            return View("Index", await _context.Movie.Include(x => x.UserMovie).ToListAsync());
        }

        // This method reads the comments registered given the movie ID
        private async Task<Comments> ReadCommentsOfMovieAsync(int id)
        {
            var context = new DynamoDBContext(dynamoDb);
            return await context.LoadAsync<Comments>(id);
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

            var userM = await _context.UserMovie
                .FirstOrDefaultAsync(m => m.MovieId == id);

                return View(movie);
        }

        // POST: Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var dynamoContext = new DynamoDBContext(dynamoDb);
            var movie = await _context.Movie.FindAsync(id);
            var userM = await _context.UserMovie
                .FirstOrDefaultAsync(m => m.MovieId == id);
            // Delete comments for that movie
            await dynamoContext.DeleteAsync<Comments>(movie.Id);
            _context.Movie.Remove(movie);
            _context.UserMovie.Remove(userM);
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int id)
        {
            return _context.Movie.Any(e => e.Id == id);
        }
    }
}
