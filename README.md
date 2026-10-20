# HỆ THỐNG QUẢN LÝ ĐIỂM VÀ ĐÁNH GIÁ CHUẨN ĐẦU RA (NOSQL V2)

> **Đồ án cuối kỳ môn Cơ sở dữ liệu NoSQL**  
> **Khoa Công nghệ Thông tin - Trường Đại học Công Thương TP.HCM (HUIT)**  
> **Công nghệ:** ASP.NET Core 9.0 Web API · MongoDB · JWT Authentication · HTML5 / Bootstrap 5 / Chart.js

---

## 📌 1. Giới thiệu tổng quan
Hệ thống quản lý điểm và chuẩn đầu ra được xây dựng theo kiến trúc **Document Database (MongoDB)** kết hợp **RESTful Web API (.NET 9)**, phục vụ 3 nhóm đối tượng: **Quản trị viên (Admin)**, **Giảng viên** và **Sinh viên**.

Hệ thống tối ưu hóa đặc trưng NoSQL (mô hình nhúng Document, mảng lồng `BangDiem`, toán tử truy vấn đệ quy `$graphLookup`, và Aggregation Pipeline phân tích thống kê thời gian thực).

---

## 🚀 2. Các nghiệp vụ & Tính năng nổi bật của Phiên bản V2

### 1. Phân quyền nhập/sửa điểm & Lịch sử chỉnh sửa điểm (Audit Log)
* **Kiểm soát quyền hạn:** Admin có thể linh hoạt mở hoặc khóa quyền nhập/sửa điểm cho từng lớp học phần (`ChoPhepGvNhapSuaDiem`). Khi chưa mở, giảng viên chỉ có quyền xem mà không thể tự ý sửa điểm.
* **Audit Trail (Nhật ký kiểm toán):** Mọi thao tác sửa điểm của giảng viên hoặc admin đều được ghi nhận tự động vào mảng `LichSuSuaDiem` (lưu vết thời gian UTC, người thực hiện, vai trò, điểm cũ $\rightarrow$ điểm mới, lý do điều chỉnh).

### 2. Môn học tiên quyết & Cây điều kiện tiên quyết (`$graphLookup`)
* **Truy vấn đệ quy NoSQL:** Tận dụng toán tử `$graphLookup` trong MongoDB Aggregation Pipeline để duyệt toàn bộ đồ thị cây môn học tiên quyết nhiều tầng chỉ với **1 câu truy vấn duy nhất** (không bị bài toán $N+1$ query như SQL quan hệ).
* **Kiểm tra điều kiện đăng ký:** Sinh viên đăng ký lớp học phần mới sẽ bị hệ thống tự động kiểm tra: nếu chưa hoàn thành các môn học tiên quyết (với điểm tổng kết $\ge 4.0$), hệ thống sẽ chặn đăng ký và xuất cảnh báo chi tiết.

### 3. Hệ thống Cảnh báo học vụ (Academic Warning System)
* Tự động quét và đánh giá học lực sinh viên dựa trên **GPA tích lũy** và **số môn nợ**:
  * **Cảnh báo Mức 1:** GPA < 2.0 hoặc nợ từ 2 môn trở lên.
  * **Cảnh báo Mức 2 (Nguy cơ đình chỉ):** GPA < 1.0 hoặc nợ từ 3 môn trở lên.
* Bảng thống kê hiển thị trực quan trên giao diện báo cáo kèm danh sách các môn chưa đạt.

### 4. Đánh giá Chuẩn đầu ra Môn học (CLO - Course Learning Outcomes)
* Thiết kế mô hình mảng nhúng CLO trong từng môn học và bảng điểm sinh viên (`CLO1, CLO2, CLO3...`).
* Phân tích và biểu diễn trực quan tỷ lệ % sinh viên đạt từng chuẩn đầu ra theo môn học và lớp học phần.

### 5. Import / Export dữ liệu theo Lớp học phần
* Xuất và nạp danh sách sinh viên theo từng **Lớp học phần** cụ thể bằng tệp JSON.
* **Thuật toán Merge thông minh:** Khi import sinh viên theo lớp học phần, hệ thống chỉ cập nhật/bổ sung môn học đó vào mảng `BangDiem`, **bảo toàn 100% điểm các môn học khác** của sinh viên trong cơ sở dữ liệu.
* Tích hợp tệp JSON mẫu chuẩn phục vụ thuyết trình và báo cáo.

---

## 🛠 3. Cấu trúc thư mục dự án

```text
├── Controllers/              # RESTful API Controllers (.NET 9)
│   ├── AuthController.cs     # Xác thực JWT & Đổi mật khẩu
│   ├── SinhVienController.cs # Quản lý SV, đăng ký LHP, tính điểm, Audit Log
│   ├── LopHocPhanController.cs # Quản lý LHP, phân công GV, mở/khóa điểm
│   ├── MonHocController.cs   # Quản lý Môn học, $graphLookup cây tiên quyết
│   ├── GiangVienController.cs# Quản lý Giảng viên
│   ├── TaiKhoanController.cs # Quản trị tài khoản & phân quyền RBAC
│   ├── ReportsController.cs  # Thống kê GPA, CLO, Cảnh báo học vụ
│   └── ImportExportController.cs # Xuất/Nhập dữ liệu theo Lớp học phần
├── Models/                   # C# Data Models & MongoDB BSON Mapping
├── Services/                 # Business Logic & MongoDB Driver Context
│   ├── MongoDbService.cs     # Kết nối MongoDB Database
│   └── GradeCalculator.cs    # Tính điểm hệ 10, hệ 4, xếp loại, CLO, cảnh báo
├── database_mongodb/         # Dữ liệu mẫu JSON để nạp vào MongoDB
│   ├── doan_cuoiky.sinhvien.json
│   ├── doan_cuoiky.lophocphan.json
│   ├── doan_cuoiky.monhoc.json
│   ├── doan_cuoiky.giangvien.json
│   ├── doan_cuoiky.taikhoan.json
│   └── mau_import_sinhvien_lophocphan.json
├── wwwroot/                  # Single Page Application (HTML, CSS, JS, Bootstrap 5)
│   ├── index.html            # Trang chủ tổng quan & Dashboard
│   ├── sinhvien.html         # Quản lý sinh viên & Bảng điểm chi tiết
│   ├── lophocphan.html       # Quản lý lớp học phần & Quyền sửa điểm
│   ├── monhoc.html           # Quản lý môn học & Cây tiên quyết
│   ├── baocao.html           # Báo cáo GPA, Cảnh báo học vụ, CLO
│   ├── importexport.html     # Import/Export JSON theo Lớp học phần
│   ├── js/app.js             # Logic xử lý gọi API & giao diện
│   └── mau_import_sinhvien_lophocphan.json
├── appsettings.json          # Cấu hình ConnectionString MongoDB & JWT Secret
├── doan_cuoiky_nosql.csproj  # File cấu hình Project .NET 9
└── README.md
```

---

## 💻 4. Hướng dẫn cài đặt và Chạy thử nghiệm

### Yêu cầu hệ thống:
* **.NET SDK 9.0** trở lên.
* **MongoDB Server 6.0+** đang chạy trên cổng mặc định `localhost:27017`.

### Các bước khởi chạy:
1. **Khởi động MongoDB:** Đảm bảo dịch vụ MongoDB trên máy tính đã được bật.
2. **Nạp dữ liệu mẫu ban đầu (nếu cần):**
   Sử dụng công cụ `mongoimport` để nạp các tệp JSON trong thư mục `database_mongodb/` vào database `doan_cuoiky_v2`.
3. **Chạy ứng dụng:**
   ```bash
   dotnet restore
   dotnet run
   ```
4. **Truy cập ứng dụng:**
   Mở trình duyệt web tại địa chỉ: `http://localhost:5000` (hoặc cổng cấu hình trong `launchSettings.json`).

### Tài khoản mặc định:
* **Quản trị viên (Admin):** `admin` / `Admin@123`
* **Giảng viên:** `GV001` / `123456`
* **Sinh viên:** `2001230396` / `123456`
