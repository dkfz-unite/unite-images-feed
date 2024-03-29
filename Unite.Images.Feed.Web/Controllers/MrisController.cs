using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unite.Images.Feed.Data;
using Unite.Images.Feed.Web.Configuration.Constants;
using Unite.Images.Feed.Web.Models;
using Unite.Images.Feed.Web.Models.Binders;
using Unite.Images.Feed.Web.Services;

namespace Unite.Images.Feed.Web.Controllers;

[Route("api/[controller]")]
[Authorize(Policy = Policies.Data.Writer)]
public class MrisController : ImagesControllerBase
{
    public MrisController(
        ImagesDataWriter dataWriter, 
        ImageIndexingTasksService indexingTasksService, 
        ILogger<MrisController> logger) : base(dataWriter, indexingTasksService, logger)
    {
    }

    [HttpPost("tsv")]
    [Consumes("text/tab-separated-values")]
    public IActionResult PostTsv([ModelBinder(typeof(MriImagesTsvModelsBinder))]ImageDataModel[] models)
    {
        var dataModels = _converter.Convert(models);

        return PostData(dataModels);
    }
}
