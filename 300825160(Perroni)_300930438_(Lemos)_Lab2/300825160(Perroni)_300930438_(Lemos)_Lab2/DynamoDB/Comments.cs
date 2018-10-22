using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace _300825160_Perroni__300930438__Lemos__Lab2.DynamoDB
{
    [DynamoDBTable("comments")]
    public class Comments
    {
        [DynamoDBProperty("Id")]
        public int movieId { get; set; }
        public List<UserComment> userComment { get; set; }
    }

    public class UserComment
    {
        
        public string Id { get; set; }
        public string userId { get; set; }
        public string comment { get; set; }
    }
}
