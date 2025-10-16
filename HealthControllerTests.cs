using Microsoft.AspNetCore.Mvc;
using WebApp.Controllers;
using Xunit;

namespace UnitTests
{
    public class HealthControllerTests
    {
        [Fact]
        public void Get_ReturnsHealthyStatus()
        {
            // Arrange
            var controller = new HealthController();

            // Act
            var result = controller.Get() as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }
    }
}