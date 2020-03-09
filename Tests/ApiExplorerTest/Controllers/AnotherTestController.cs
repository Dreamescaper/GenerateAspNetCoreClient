using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnotherTestController : ControllerBase
    {
        [HttpGet("with-query-model")]
        public async Task<ActionResult> WithQueryModel([FromQuery]SomeQueryModel queryModel)
        {
            await Task.Delay(1);
            return Ok();
        }
    }
}