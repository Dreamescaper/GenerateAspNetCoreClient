using Microsoft.AspNetCore.Mvc;

namespace TestWebApi.Versioning.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiVersion("1.0", Deprecated = true)]
    [ApiVersion("2.0", Deprecated = true)]
    [ApiVersion("1.5", Deprecated = true)]
    [ApiVersion("3.0")]
    [ApiVersion("4.0")]
    public class VersionController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("test2");
        }

        [HttpGet]
        [MapToApiVersion("3.0")]
        public IActionResult Get3()
        {
            return Ok("test3");
        }

        [HttpGet]
        [MapToApiVersion("4.0")]
        public IActionResult Get4([FromServices] ApiVersion apiVersion)
        {
            return Ok("test" + apiVersion.MajorVersion);
        }
    }
}