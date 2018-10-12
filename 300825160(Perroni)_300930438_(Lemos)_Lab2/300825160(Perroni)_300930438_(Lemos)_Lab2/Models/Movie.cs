using System;
using System.Collections.Generic;

namespace _300825160_Perroni__300930438__Lemos__Lab2.Models
{
    public partial class Movie
    {
        public Movie()
        {
            UserMovie = new HashSet<UserMovie>();
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string Sinopsis { get; set; }
        public string Genre { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int Duration { get; set; }

        public ICollection<UserMovie> UserMovie { get; set; }
    }
}
