using Employee_Client.Shared.Model;
using Employee_Client.Shared.Model.complaint;
using Refit;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Employee_Client.Shared.AuthApiService;
//using Employee_Client.Shared.Models;
namespace Employee_Client.Shared.Services
{

    //[Headers("User-Agent: Your App", "Authorization: Bearer")]
    internal interface IApiRefit
    {
        [Post("/login2?category={category}&UserID={UserID}&LoginPwd={LoginPwd}")]//?Comp_code={Comp_code}&Machine_no={Machine_no}&date={date}&shift={shift}")]
        //Task<List<Professional>> Bind(string Comp_code, string Machine_no, string date,string shift);
        Task<HttpResponseMessage> login2(string? category, string? UserID, string? LoginPwd);

        [Get("/menu/{moduleCode}")]
        Task<HttpResponseMessage> menu(string? moduleCode);
        [Get("/clients/{CompCode}")]
        Task<HttpResponseMessage> clients(string? CompCode="CVSPL");
        [Get("/projects/{moduleCode}/{clientCode}")]
        Task<HttpResponseMessage> projects(string? moduleCode,string? clientCode);
        [Get("/projectdetails/{CompCode}/{clientCode}/{projectCode}")]
        Task<HttpResponseMessage> projectdetails(string? CompCode, string? clientCode, string? projectCode);
        [Get("/complaints/{compCode}")]
        Task<HttpResponseMessage> complaints(string? compCode);
        [Get("/getemailid/{loginId}")]
        Task<HttpResponseMessage> getemailid(string? loginId);
        [Get("/download-file/{path}")]
        Task<HttpResponseMessage> downloadfile(string? path);
        [Get("/report")]
        Task<HttpResponseMessage> report();















        [Post("/saveComp/{currentUserId}")]
        Task<HttpResponseMessage> SaveComplaint(string currentUserId, [Body] ComplaintDto model);
        [Post("/saveComp_update/{currentUserId}")]
        Task<HttpResponseMessage> saveComp_update(string currentUserId, [Body] ComplaintDto m);
        [Post("/upload-file")]
        Task<HttpResponseMessage> uploadfile();
        [Post("/verify-otp")]
        Task<HttpResponseMessage> verifyotp(VerifyOtpDto dto);
        [Post("/validate-password")]
        Task<HttpResponseMessage> validatepassword(ResetPasswordDto dto);
        [Post("/reset")]
        Task<HttpResponseMessage> reset(ResetPasswordDto dto);
        [Post("/reset_loggedin")]
        Task<HttpResponseMessage> reset_loggedin(ResetPasswordDto dto);
        [Delete("/deleteComplaint/{id}")]
        Task<HttpResponseMessage> deleteComplaint(int id);
        [Get("/editComplaint/{m}")]
        Task<HttpResponseMessage> editComplaint(int m);


        [Get("/getKnowledgeBase/{currentUserId}")]
        Task<List<string>> GetKnowledgeBaseRecords(string currentUserId);
        ////[Headers("Content-Type: application/x-www-form-urlencoded")]
        ////[Headers("Content-Type: text/xml; encoding=utf-8")]
        //[Get("/Bind?Comp_code={Comp_code}&Machine_no={Machine_no}&date={date}&shift={shift}")]
        ////Task<List<Professional>> Bind(string Comp_code, string Machine_no, string date,string shift);
        //Task<HttpResponseMessage> Bind(string Comp_code, string Machine_no, string date, string shift);

        //[Get("/getMachines?Comp_code={Comp_code}")]
        //Task<HttpResponseMessage> getMachines(string Comp_code);
        //[Get("/get_Nuttype?Comp_code={Comp_code}&Machine_no={Machine_no}")]
        //Task<HttpResponseMessage> get_Nuttype(string Comp_code, string Machine_no);
        //[Get("/getEmployees?Comp_code={Comp_code}&Emp_no={Emp_no}")]
        //Task<HttpResponseMessage> getEmployees(string Comp_code,string Emp_no);

        //[Get("/get_By?Comp_code={Comp_code}&Machine_no={Machine_no}&Input_Date={Input_Date}&Input_Shift={Input_Shift}")]
        //Task<HttpResponseMessage> get_By(string Comp_code, string Machine_no, string Input_Date, string Input_Shift);

        //[Get("/Save?Comp_code={Comp_code}&Machine_no={Machine_no}&serial_no={serial_no}&Input_Date={Input_Date}&Input_Shift={Input_Shift}&cls={cls}&Actual_Value={Actual_Value}")]
        //Task<HttpResponseMessage> Save(string Comp_code, string Machine_no, string serial_no, string Input_Date, string Input_Shift, string cls, string Actual_Value);

        //[Get("/Save_insert_main_table?Comp_code={Comp_code}&Machine_no={Machine_no}&Input_Date={Input_Date}")]
        //Task<HttpResponseMessage> Save_insert_main_table(string Comp_code, string Machine_no, string Input_Date);

        //[Get("/Save_checkedby?Comp_code={Comp_code}&Machine_no={Machine_no}&Input_Date={Input_Date}&Emp_no={Emp_no}")]
        //Task<HttpResponseMessage> Save_checkedby(string Comp_code, string Machine_no, string Input_Date, string Emp_no);

        //[Get("/Save_approvedby?Comp_code={Comp_code}&Machine_no={Machine_no}&Input_Date={Input_Date}&Emp_no={Emp_no}")]
        //Task<HttpResponseMessage> Save_approvedby(string Comp_code, string Machine_no, string Input_Date, string Emp_no);

        //[Get("/Save_inspectedby?Comp_code={Comp_code}&Machine_no={Machine_no}&Input_Date={Input_Date}&Input_Shift={Input_Shift}&Emp_no={Emp_no}")]
        //Task<HttpResponseMessage> Save_inspectedby(string Comp_code, string Machine_no, string Input_Date, string Input_Shift, string Emp_no);


        ////Task<DataTable> Bind(string Comp_code, string Machine_no, string date);
        [Get("/search")]
        Task<HttpResponseMessage> GetWeightedSearchResultsAsync([Query] string compCode, [AliasAs("term")] string? searchTerm = "", [Query] string? dateFormat = "dd-MM-yyyy");

        [Post("/search/weight")]
        Task IncrementClickWeightAsync([Body] UpdateWeightRequest request);
        [Get("/topweighted")]
        Task<HttpResponseMessage> GetTopWeightedItemsAsync();










        [Get("/chat/users/{currentUserId}")]
        Task<HttpResponseMessage> GetChatUsers(string currentUserId);

        [Get("/chat/messages/{user1}/{user2}")]
        Task<HttpResponseMessage> GetChatMessages(string user1, string user2, [Query] int skip, [Query] int take);

        [Post("/chat/send")]
        Task<HttpResponseMessage> SendChatMessage([Body] ChatMessageDto msg);

        [Post("/chat/markread")]
        Task<HttpResponseMessage> MarkMessagesAsRead([Body] MarkReadDto request);

        [Get("/alerts/{userId}")]
        Task<HttpResponseMessage> GetUnreadAlerts(string userId);

        [Post("/alerts/markread/{alertId}")]
        Task<HttpResponseMessage> MarkAlertAsRead(int alertId);













        //[Get("/complaints/paginated/{CompCode}")]
        //Task<HttpResponseMessage> GetPaginatedComplaints(string CompCode, [Query] int pageNumber, [Query] int pageSize);
        // Inside IApiRefit.cs

        [Get("/Complaints/paginated/{CompCode}")]
        Task<HttpResponseMessage> GetPaginatedComplaints(
    string CompCode, [Query] int pageNumber, [Query] int pageSize, [Query] string searchTerm, [Query] string searchCols, [Query] string sortCols);

        [Get("/short-date-format")]
        Task<string> GetShortDateFormatAsync();







        [Post("/save-column-state")]
        Task SaveColumnState([Body] ColumnStateDto dto);

        [Get("/get-column-state/{loginId}/{formName}")]
        Task<string> GetColumnState(string loginId, string formName);
    }
}
