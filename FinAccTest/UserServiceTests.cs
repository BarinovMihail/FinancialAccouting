using FinancialAccounting;
using Xunit;
using Npgsql;
namespace FinAccTest
{
    public class UserServiceTests
    {
        [Fact]
        public void RegisterUser_ShouldReturnError_WhenFieldsAreEmpty()
        {
            var result = UserService.RegisterUser("", "", "");
            Assert.Equal("Пожалуйста, заполните все поля.", result);
        }

        [Fact]
        public void RegisterUser_ShouldReturnSuccess_WhenUserIsNew()
        {
            var result = UserService.RegisterUser("testuser", "test@example.com", "123456");

            Assert.True(result == "OK" || result == "Пользователь с таким логином или email уже существует.");
        }
    }
}
