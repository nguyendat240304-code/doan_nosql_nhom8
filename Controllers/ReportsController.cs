using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,giaovien")]
public class ReportsController : ControllerBase
{
    private readonly MongoDbService _db;

    public ReportsController(MongoDbService db)
    {
        _db = db;
    }

    /// <summary>
    /// Báo cáo học lực theo lớp. GPA trọng số được tính hoàn toàn bằng Aggregation Pipeline
    /// ($unwind, $group, $project, $cond, $switch).
    /// </summary>
    [HttpGet("hocluc")]
    public async Task<IActionResult> BaoCaoHocLuc([FromQuery] string? lop)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        List<string>? myLhpList = null;

        if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            myLhpList = await _db.LopHocPhans
                .Find(x => x.GiangVien.MaGV == currentRefId)
                .Project(x => x.Id)
                .ToListAsync();

            if (!myLhpList.Any())
                return Ok(new List<object>());
        }

        var noPracticeCourseIds = await _db.MonHocs
            .Find(x => x.ThucHanh <= 0)
            .Project(x => x.Id)
            .ToListAsync();

        var matchDoc = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(lop))
        {
            matchDoc.Add("LopSinhHoat", lop.Trim());
        }
        if (currentRole == "giaovien" && myLhpList != null)
        {
            matchDoc.Add("BangDiem.MaLHP", new BsonDocument("$in", new BsonArray(myLhpList)));
        }

        var pipeline = new List<BsonDocument>
        {
            new BsonDocument("$match", matchDoc),
            new BsonDocument("$addFields", new BsonDocument("BangDiem", new BsonDocument("$filter", new BsonDocument
            {
                { "input", new BsonDocument("$ifNull", new BsonArray { "$BangDiem", new BsonArray() }) },
                { "as", "b" },
                { "cond", TaoDieuKienCoDiem(noPracticeCourseIds, currentRole == "giaovien" ? myLhpList : null) }
            }))),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$BangDiem" },
                { "preserveNullAndEmptyArrays", true }
            })
        };

        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$_id" },
            { "HoTen", new BsonDocument("$first", "$HoTen") },
            { "LopSinhHoat", new BsonDocument("$first", "$LopSinhHoat") },
            { "KhoaHoc", new BsonDocument("$first", "$KhoaHoc") },
            {
                "TongDiemHeSo",
                new BsonDocument("$sum", new BsonDocument("$multiply", new BsonArray
                {
                    "$BangDiem.DiemTongKet",
                    "$BangDiem.SoTinChi"
                }))
            },
            { "TongTinChi", new BsonDocument("$sum", "$BangDiem.SoTinChi") },
            { "SoMonCoDiem", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
            {
                new BsonDocument("$ifNull", new BsonArray { "$BangDiem.MaLHP", false }),
                1,
                0
            })) }
        }));

        pipeline.Add(new BsonDocument("$project", new BsonDocument
        {
            { "HoTen", 1 },
            { "LopSinhHoat", 1 },
            { "KhoaHoc", 1 },
            { "TongTinChi", 1 },
            { "SoMonCoDiem", 1 },
            {
                "GPA",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$or", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$SoMonCoDiem", 0 }),
                        new BsonDocument("$eq", new BsonArray { "$TongTinChi", 0 })
                    }),
                    BsonNull.Value,
                    new BsonDocument("$round", new BsonArray
                    {
                        new BsonDocument("$divide", new BsonArray { "$TongDiemHeSo", "$TongTinChi" }),
                        2
                    })
                })
            }
        }));

        pipeline.Add(new BsonDocument("$project", new BsonDocument
        {
            { "HoTen", 1 },
            { "LopSinhHoat", 1 },
            { "KhoaHoc", 1 },
            { "TongTinChi", 1 },
            { "GPA", 1 },
            {
                "XepLoai",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$SoMonCoDiem", 0 }),
                    "Chưa có điểm",
                    new BsonDocument("$switch", new BsonDocument
                    {
                        { "branches", new BsonArray
                        {
                            new BsonDocument
                            {
                                { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 9.0 }) },
                                { "then", "Xuất sắc" }
                            },
                            new BsonDocument
                            {
                                { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 8.0 }) },
                                { "then", "Giỏi" }
                            },
                            new BsonDocument
                            {
                                { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 7.0 }) },
                                { "then", "Khá" }
                            },
                            new BsonDocument
                            {
                                { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 5.0 }) },
                                { "then", "Trung bình" }
                            }
                        } },
                        { "default", "Yếu" }
                    })
                })
            }
        }));

        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { "LopSinhHoat", 1 },
            { "GPA", -1 }
        }));

        var result = await _db.SinhViens.Aggregate<BsonDocument>(pipeline).ToListAsync();
        var json = result.Select(d =>
        {
            var dict = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Dictionary<string, object>>(d) ?? new Dictionary<string, object>();
            if (d.Contains("_id") && !dict.ContainsKey("Id"))
            {
                dict["Id"] = d["_id"].ToString() ?? string.Empty;
            }
            return dict;
        });

        return Ok(json);
    }

    private static BsonDocument TaoDieuKienCoDiem(IReadOnlyCollection<string> noPracticeCourseIds, List<string>? maLhpList)
    {
        var complete = GradeCalculator.CompletedCourseCondition(noPracticeCourseIds);
        if (maLhpList == null) return complete;
        return new BsonDocument("$and", new BsonArray
        {
            complete,
            new BsonDocument("$in", new BsonArray { "$$b.MaLHP", new BsonArray(maLhpList) })
        });
    }

    [HttpGet("lopsinhhoat")]
    public async Task<IActionResult> DanhSachLop()
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var filter = Builders<Models.SinhVien>.Filter.Empty;

        if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var myLhpList = await _db.LopHocPhans
                .Find(x => x.GiangVien.MaGV == currentRefId)
                .Project(x => x.Id)
                .ToListAsync();

            if (!myLhpList.Any())
                return Ok(new List<string>());

            filter = Builders<Models.SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => myLhpList.Contains(b.MaLHP));
        }

        var lops = await _db.SinhViens.DistinctAsync<string>("LopSinhHoat", filter);
        var list = await lops.ToListAsync();
        list.Sort();
        return Ok(list);
    }

    /// <summary>
    /// Thống kê danh sách sinh viên bị Cảnh báo học vụ (GPA < 2.0 hoặc nợ môn)
    /// </summary>
    [HttpGet("canhbaohocvu")]
    public async Task<IActionResult> BaoCaoCanhBaoHocVu([FromQuery] string? lop)
    {
        var filter = string.IsNullOrWhiteSpace(lop)
            ? Builders<SinhVien>.Filter.Empty
            : Builders<SinhVien>.Filter.Eq(x => x.LopSinhHoat, lop);

        var sinhViens = await _db.SinhViens.Find(filter).ToListAsync();
        var danhSach = new List<object>();

        foreach (var sv in sinhViens)
        {
            var monCoDiem = sv.BangDiem.Where(b => b.DiemTongKet.HasValue && b.SoTinChi > 0).ToList();
            if (!monCoDiem.Any()) continue;

            double tongDiemHeSo = monCoDiem.Sum(b => b.DiemTongKet!.Value * b.SoTinChi);
            int tongTinChi = monCoDiem.Sum(b => b.SoTinChi);
            double gpa = tongTinChi > 0 ? Math.Round(tongDiemHeSo / tongTinChi, 2) : 0;
            int soMonNo = monCoDiem.Count(b => b.DiemTongKet!.Value < 4.0);

            var mucCanhBao = GradeCalculator.TinhCanhBaoHocVu(gpa, soMonNo);
            if (mucCanhBao != "Bình thường")
            {
                int mucDoNum = mucCanhBao.Contains("2") ? 2 : 1;
                var monNoList = monCoDiem.Where(b => b.DiemTongKet!.Value < 4.0).Select(b => $"{b.TenMon} ({b.DiemTongKet:0.0})").ToList();

                danhSach.Add(new
                {
                    Id = sv.Id,
                    HoTen = sv.HoTen,
                    LopSinhHoat = sv.LopSinhHoat,
                    GPA = gpa,
                    SoMonNo = soMonNo,
                    MucCanhBao = mucCanhBao,
                    CacMonNo = monNoList,
                    sinhVien = new { Id = sv.Id, HoTen = sv.HoTen, LopSinhHoat = sv.LopSinhHoat },
                    canhBao = new
                    {
                        GpaHienTai = gpa,
                        SoMonNo = soMonNo,
                        MucDo = mucDoNum,
                        MucCanhBao = mucCanhBao,
                        LyDo = mucDoNum == 2 ? "GPA < 1.0 hoặc nợ từ 3 môn trở lên" : "GPA < 2.0 hoặc nợ từ 2 môn trở lên",
                        CacMonChuaDat = monNoList
                    }
                });
            }
        }

        return Ok(new
        {
            thongKe = new
            {
                tongSo = danhSach.Count,
                muc1 = danhSach.Count(x => ((dynamic)x).MucCanhBao.Contains("1")),
                muc2 = danhSach.Count(x => ((dynamic)x).MucCanhBao.Contains("2"))
            },
            danhSach = danhSach
        });
    }
}
