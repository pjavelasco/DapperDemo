using Dapper;
using DapperDemo.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Transactions;

namespace DapperDemo.Repository
{
    public class BonusRepository : IBonusRepository
    {
        private readonly IDbConnection _db;

        public BonusRepository(IConfiguration configuration)
        {
            _db = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        }

        public void AddTestCompanyWithEmployees(Company objComp)
        {
            var sql = "INSERT INTO Companies (Name, Address, City, State, PostalCode) VALUES(@Name, @Address, @City, @State, @PostalCode);"
            + "SELECT CAST(SCOPE_IDENTITY() as int); ";
            var id = _db.Query<int>(sql, objComp).Single();
            objComp.CompanyId = id;

            //foreach (var employee in objComp.Employees)
            //{
            //    employee.CompanyId = objComp.CompanyId;
            //    var sql1 = "INSERT INTO Employees (Name, Title, Email, Phone, CompanyId) VALUES(@Name, @Title, @Email, @Phone, @CompanyId);" +
            //        "SELECT CAST(SCOPE_IDENTITY() as int);";
            //    _db.Query<int>(sql1, employee).Single();               
            //}

            objComp.Employees.Select(c =>
            {
                c.CompanyId = id;
                return c;
            }).ToList();

            var sql1 = "INSERT INTO Employees (Name, Title, Email, Phone, CompanyId) VALUES(@Name, @Title, @Email, @Phone, @CompanyId);" +
                "SELECT CAST(SCOPE_IDENTITY() as int);";

            _db.Execute(sql1, objComp.Employees);

        }

        public void AddTestCompanyWithEmployeesWithTransaction(Company objComp)
        {
            using (var transaction = new TransactionScope())
            {
                try
                {
                    var sql = "INSERT INTO Companies (Name, Address, City, State, PostalCode) VALUES(@Name, @Address, @City, @State, @PostalCode);"
                    + "SELECT CAST(SCOPE_IDENTITY() as int); ";
                    var id = _db.Query<int>(sql, objComp).Single();
                    objComp.CompanyId = id;


                    objComp.Employees.Select(c =>
                    {
                        c.CompanyId = id;
                        return c;
                    }).ToList();

                    var sql1 = "INSERT INTO Employees (Name, Title, Email, Phone, CompanyId) VALUES(@Name, @Title, @Email, @Phone, @CompanyId);" +
                        "SELECT CAST(SCOPE_IDENTITY() as int);";

                    _db.Execute(sql1, objComp.Employees);

                    transaction.Complete();
                }
                catch (System.Exception)
                {

                    
                }
            }


        }


        public List<Company> FilterCompanyByName(string name)
        {
            return _db.Query<Company>("SELECT * FROM Companies WHERE Name LIKE '%' + @name + '%'", new { name }).ToList();
        }

        public List<Company> GetAllCompaniesWithEmployees()
        {
            var sql = "SELECT C.*, E.* FROM Employees AS E INNER JOIN Companies AS C ON E.CompanyId = C.CompanyId";
            var companyDictionary = new Dictionary<int, Company>();

            var company = _db.Query<Company, Employee, Company>(sql, (c, e) => {
                if (!companyDictionary.TryGetValue(c.CompanyId, out var companyObj))
                {
                    companyObj = c;
                    companyDictionary.Add(companyObj.CompanyId, companyObj);
                }
                companyObj.Employees.Add(e);
                return companyObj;
            }, splitOn: "EmployeeId").Distinct().ToList();

            return company;
        }

        public Company GetCompanyWithEmployees(int id)
        {
            var p = new 
            {
                CompanyId = id
            };

            var sql = "SELECT * FROM Companies WHERE CompanyId = @CompanyId"
                + " SELECT * FROM Employees WHERE CompanyId = @CompanyId";

            Company company;

            using (var lists = _db.QueryMultiple(sql, p))
            {
                company = lists.Read<Company>().ToList().FirstOrDefault();
                company.Employees = lists.Read<Employee>().ToList();
            }

            return company;
        }

        public List<Employee> GetEmployeeWithCompany(int id)
        {
            var sql = "SELECT * FROM Employees AS E INNER JOIN Companies AS C ON E.CompanyId = C.CompanyId";
            if (id != 0)
            {
                sql += " WHERE E.CompanyId = @id";
            }
            
            return _db.Query<Employee, Company, Employee>(sql, (employee, company) =>
            {
                employee.Company = company;
                return employee;
            }, new { id }, splitOn:"CompanyId").ToList();
        }

        public void RemoveRange(int[] companyId)
        {
            _db.Query("DELETE FROM Companies WHERE CompanyId IN @companyId", new { companyId });
        }
    }
}
