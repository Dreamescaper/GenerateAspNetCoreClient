using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ApiExplorerWebApiTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnotherTestController : ControllerBase
    {
        [HttpGet("with-query-model")]
        public async Task<ActionResult> WithQueryModel([FromQuery]SomeQueryModel queryModel)
        {
            return Ok();
        }
    }
}