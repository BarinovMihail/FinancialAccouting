using FinancialAccounting.Class;
using Moq.Protected;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FinAccTest
{
    public class MistralServiceTests
    {
        [Fact]
        public async Task GetAnalysisAsync_ReturnsExpectedResult()
        {
            var expectedResponse = "{\"choices\":[{\"message\":{\"content\":\"Анализ завершён успешно.\"}}]}";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedResponse),
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var service = new MistralService(httpClient);
            var result = await service.GetAnalysisAsync("тестовые данные");
            Assert.Equal("Анализ завершён успешно.", result);
        }
    }
}
