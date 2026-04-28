using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class WarrantyViewModelTests
    {
        [Fact]
        public void CreateWarrantyClaimPassesFormValuesToService()
        {
            string? claimCode = null;
            string? serialNumber = null;
            string? problemDescription = null;
            int? receivedBy = null;

            var viewModel = new WarrantyViewModel(
                new Employee { Id = 42, FullName = "Nhan vien" },
                (code, serial, problem, userId) =>
                {
                    claimCode = code;
                    serialNumber = serial;
                    problemDescription = problem;
                    receivedBy = userId;
                    return 123;
                },
                (_, _) => { });
            viewModel.ClaimCode = "WC-001";
            viewModel.SerialNumber = "SERIAL-001";
            viewModel.ProblemDescription = "Loi man hinh";

            viewModel.CreateWarrantyClaimCommand.Execute(null);

            Assert.Equal("WC-001", claimCode);
            Assert.Equal("SERIAL-001", serialNumber);
            Assert.Equal("Loi man hinh", problemDescription);
            Assert.Equal(42, receivedBy);
            Assert.Equal("Da tao phieu bao hanh #123.", viewModel.StatusMessage);
            Assert.Equal(string.Empty, viewModel.SerialNumber);
            Assert.Equal(string.Empty, viewModel.ProblemDescription);
        }
    }
}
