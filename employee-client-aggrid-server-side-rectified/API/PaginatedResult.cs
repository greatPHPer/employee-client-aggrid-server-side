namespace Employee_Client.Shared.Model.complaint
{
    // 1. Add this helper class at the bottom of AuthController.cs (or in your Models folder)
    public class PaginatedResult<T>
    {
        public List<T> Data { get; set; } = new();
        public int TotalRecords { get; set; }
    }
}
