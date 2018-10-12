using System;
using System.Collections.Generic;

namespace _300825160_Perroni__300930438__Lemos__Lab2.Models
{
    public partial class UserMovie
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int MovieId { get; set; }

        public Movie Movie { get; set; }
        public AspNetUsers User { get; set; }
    }
}
