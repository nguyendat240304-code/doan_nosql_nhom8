using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class ImportExportController : ControllerBase
{
    private readonly MongoDbService _db;

    public ImportExportController(MongoDbService db) => _db = db;

    [HttpGet("sinhvien")]
    public async Task<IActionResult> ExportSinhVien([FromQuery] string? maLhp, [FromQuery] string? lop)
    {
        FilterDefinition<SinhVien> filter = FilterDefinition<SinhVien>.Empty;

        if (!string.IsNullOrWhiteSpace(maLhp))
        {
            var cleanLhp = maLhp.Trim();
            filter = Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == cleanLhp);
        }
        else if (!string.IsNullOrWhiteSpace(lop))
        {
            filter = Builders<SinhVien>.Filter.Eq(x => x.LopSinhHoat, lop.Trim());
        }

        var list = await _db.SinhViens.Find(filter).ToListAsync();
        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = !string.IsNullOrWhiteSpace(maLhp)
            ? $"sinhvien_{maLhp.Trim().Replace(" ", "_")}.json"
            : (!string.IsNullOrWhiteSpace(lop) ? $"bangdiem_{lop.Trim()}.json" : "bangdiem_sinhvien.json");
        return File(bytes, "application/json", fileName);
    }

    [HttpGet("mau-json")]
    [AllowAnonymous]
    public IActionResult DownloadMauJson()
    {
        var sampleFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "mau_import_sinhvien_lophocphan.json");
        if (!System.IO.File.Exists(sampleFile))
        {
            sampleFile = Path.Combine(Directory.GetCurrentDirectory(), "database_mongodb", "mau_import_sinhvien_lophocphan.json");
        }
        if (System.IO.File.Exists(sampleFile))
        {
            var bytes = System.IO.File.ReadAllBytes(sampleFile);
            return File(bytes, "application/json", "mau_import_sinhvien_lophocphan.json");
        }
        return NotFound("Chưa tìm thấy file JSON mẫu.");
    }

    [HttpPost("sinhvien")]
    public async Task<IActionResult> ImportSinhVien(IFormFile file, [FromQuery] string? maLhp)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Chưa chọn file JSON.");

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        List<SinhVien>? students;
        try
        {
            students = JsonSerializer.Deserialize<List<SinhVien>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"JSON không hợp lệ: {ex.Message}");
        }

        if (students is null || students.Count == 0)
            return BadRequest("File không chứa sinh viên.");

        LopHocPhan? targetLhp = null;
        if (!string.IsNullOrWhiteSpace(maLhp))
        {
            targetLhp = await _db.LopHocPhans.Find(x => x.Id == maLhp.Trim()).FirstOrDefaultAsync();
            if (targetLhp == null)
                return BadRequest($"Lớp học phần '{maLhp}' không tồn tại trong hệ thống.");
        }

        var importedCount = students.Count;
        var studentsToImport = students.GroupBy(x => x.Id).Select(x => x.Last()).ToList();
        if (studentsToImport.Any(x => string.IsNullOrWhiteSpace(x.Id)))
            return BadRequest("Mỗi hồ sơ sinh viên trong JSON phải có mã sinh viên (_id hoặc Id).");

        // Nếu người dùng chọn gán vào một lớp học phần cụ thể
        if (targetLhp != null)
        {
            foreach (var student in studentsToImport)
            {
                student.BangDiem ??= new List<BangDiemMonHoc>();
                var item = student.BangDiem.FirstOrDefault(b => string.Equals(b.MaLHP?.Trim(), targetLhp.Id, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    student.BangDiem.Add(new BangDiemMonHoc
                    {
                        MaLHP = targetLhp.Id,
                        MaMon = targetLhp.MaMon,
                        TenMon = targetLhp.TenMon,
                        SoTinChi = targetLhp.SoTinChi > 0 ? targetLhp.SoTinChi : 3,
                        Hocky = targetLhp.Hocky,
                        NamHoc = targetLhp.NamHoc,
                        GiangVien = targetLhp.GiangVien ?? new GiangVienPhuTrach(),
                        DiemThanhPhan = new DiemThanhPhan()
                    });
                }
                else
                {
                    item.MaLHP = targetLhp.Id;
                    item.MaMon = targetLhp.MaMon;
                    item.TenMon = targetLhp.TenMon;
                    if (string.IsNullOrWhiteSpace(item.Hocky)) item.Hocky = targetLhp.Hocky;
                    if (string.IsNullOrWhiteSpace(item.NamHoc)) item.NamHoc = targetLhp.NamHoc;
                    if (item.GiangVien == null || string.IsNullOrWhiteSpace(item.GiangVien.MaGV))
                        item.GiangVien = targetLhp.GiangVien ?? new GiangVienPhuTrach();
                }
            }
        }

        foreach (var student in studentsToImport)
        {
            var ids = (student.BangDiem ?? new List<BangDiemMonHoc>()).Select(x => x.MaLHP).ToList();
            if (ids.Count != ids.Distinct().Count())
                return BadRequest($"Sinh viên {student.Id} có đăng ký lớp học phần trùng trong JSON.");
        }

        var studentIds = studentsToImport.Select(x => x.Id).ToList();
        var oldStudents = await _db.SinhViens.Find(x => studentIds.Contains(x.Id)).ToListAsync();
        var oldById = oldStudents.ToDictionary(x => x.Id);

        // Hợp nhất dữ liệu sinh viên: giữ lại các môn học khác không nằm trong file import
        foreach (var student in studentsToImport)
        {
            if (oldById.TryGetValue(student.Id, out var oldStudent))
            {
                var newMaLhpSet = (student.BangDiem ?? new List<BangDiemMonHoc>()).Select(b => b.MaLHP).ToHashSet();
                var preservedItems = (oldStudent.BangDiem ?? new List<BangDiemMonHoc>())
                    .Where(b => !newMaLhpSet.Contains(b.MaLHP)).ToList();

                student.BangDiem ??= new List<BangDiemMonHoc>();
                student.BangDiem.AddRange(preservedItems);

                if (string.IsNullOrWhiteSpace(student.HoTen)) student.HoTen = oldStudent.HoTen;
                if (string.IsNullOrWhiteSpace(student.Email)) student.Email = oldStudent.Email;
                if (string.IsNullOrWhiteSpace(student.LopSinhHoat)) student.LopSinhHoat = oldStudent.LopSinhHoat;
                if (string.IsNullOrWhiteSpace(student.MaKhoa)) student.MaKhoa = oldStudent.MaKhoa;
                if (string.IsNullOrWhiteSpace(student.KhoaHoc)) student.KhoaHoc = oldStudent.KhoaHoc;
                if (string.IsNullOrWhiteSpace(student.TrangThai)) student.TrangThai = oldStudent.TrangThai;
                if (student.NgaySinh == null) student.NgaySinh = oldStudent.NgaySinh;
                if (string.IsNullOrWhiteSpace(student.GioiTinh)) student.GioiTinh = oldStudent.GioiTinh;
                if (string.IsNullOrWhiteSpace(student.DienThoai)) student.DienThoai = oldStudent.DienThoai;
            }
        }

        var enrollmentDeltas = new Dictionary<string, int>();
        foreach (var student in studentsToImport)
        {
            if (oldById.TryGetValue(student.Id, out var oldStudent))
            {
                foreach (var idLhp in (oldStudent.BangDiem ?? new List<BangDiemMonHoc>()).Select(x => x.MaLHP).Distinct())
                    enrollmentDeltas[idLhp] = enrollmentDeltas.GetValueOrDefault(idLhp) - 1;
            }

            foreach (var idLhp in (student.BangDiem ?? new List<BangDiemMonHoc>()).Select(x => x.MaLHP).Distinct())
                enrollmentDeltas[idLhp] = enrollmentDeltas.GetValueOrDefault(idLhp) + 1;
        }

        var currentCounts = await _db.GetEnrollmentCountsAsync();
        var touchedIds = enrollmentDeltas.Keys.ToList();
        var touchedClasses = await _db.LopHocPhans.Find(x => touchedIds.Contains(x.Id)).ToListAsync();
        var classById = touchedClasses.ToDictionary(x => x.Id);

        foreach (var idLhp in touchedIds)
        {
            if (!classById.TryGetValue(idLhp, out var lhp))
                return BadRequest($"Không tìm thấy lớp học phần '{idLhp}' trong hệ thống.");
            var projected = currentCounts.GetValueOrDefault(idLhp) + enrollmentDeltas[idLhp];
            if (projected > lhp.SiSoToiDa && projected > currentCounts.GetValueOrDefault(idLhp))
                return Conflict($"Import sẽ vượt sĩ số tối đa lớp {idLhp} (Tối đa {lhp.SiSoToiDa}, dự kiến {projected}).");
        }

        var ops = studentsToImport.Select(sv =>
            new ReplaceOneModel<SinhVien>(Builders<SinhVien>.Filter.Eq(x => x.Id, sv.Id), sv)
            {
                IsUpsert = true
            }).ToList();

        BulkWriteResult<SinhVien> result;
        try
        {
            result = await _db.SinhViens.BulkWriteAsync(ops);
        }
        catch
        {
            await DongBoSiSoAsync(touchedIds);
            throw;
        }
        await DongBoSiSoAsync(touchedIds);

        return Ok(new
        {
            imported = importedCount,
            upserted = result.Upserts.Count,
            modified = result.ModifiedCount,
            lopHocPhan = targetLhp?.Id
        });
    }

    private async Task DongBoSiSoAsync(IEnumerable<string> maLhpIds)
    {
        var actualCounts = await _db.GetEnrollmentCountsAsync();
        foreach (var maLhp in maLhpIds)
            await _db.LopHocPhans.UpdateOneAsync(
                x => x.Id == maLhp,
                Builders<LopHocPhan>.Update.Set(x => x.SiSoHienTai, actualCounts.GetValueOrDefault(maLhp)));
    }
}
