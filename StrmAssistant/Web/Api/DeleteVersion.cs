using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/Items/{Id}/DeleteVersion", "POST")]
    [Authenticated]
    public class DeleteVersion : IReturn<DeleteVersionResult>, IReturn
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string",
            ParameterType = "path")]
        public string Id { get; set; }

        [ApiMember(Name = "DeleteParent", Description = "Delete Parent", IsRequired = false, DataType = "string",
            ParameterType = "query")]
        public bool DeleteParent { get; set; }
    }

    public class DeleteVersionResult
    {
        public string Status { get; set; }
        public int WouldDeleteCount { get; set; }
        public int DeletedCount { get; set; }
        public int SkippedCount { get; set; }
        public int RetainedStagingCount { get; set; }
        public string Error { get; set; }
    }
}
