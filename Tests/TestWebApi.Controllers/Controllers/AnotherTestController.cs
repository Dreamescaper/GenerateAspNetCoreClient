using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TestWebApi.Models;

namespace TestWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnotherTestController : ControllerBase
    {
        [HttpGet("with-query-model")]
        public async Task<ActionResult> WithQueryModel([FromQuery] SomeQueryModel queryModel)
        {
            await Task.Delay(1);
            return Ok();
        }

        [HttpGet("with-query-name")]
        public ActionResult WithQueryParameterName([FromQuery(Name = "currency")] string currencyName)
        {
            return Ok();
        }

        [HttpGet("with-query-name-array")]
        public ActionResult WithQueryArrayParameterName([FromQuery(Name = "currencies")] string[] currencyNames)
        {
            return Ok();
        }

        [HttpGet("with-query-enum")]
        public ActionResult WithQueryEnumParameter([FromQuery] SomeEnum enumParam)
        {
            return Ok();
        }
    }
}