# HƯỚNG DẪN VẬN HÀNH CHI TIẾT & DỄ HIỂU HỆ THỐNG MIGRATION
## HỆ THỐNG DI TRÚ ỨNG DỤNG ON-PREMISE LÊN CLOUD-NATIVE AWS

Tài liệu này được biên soạn bằng ngôn ngữ trực quan, dễ hiểu, kèm theo các ví dụ thực tế và giải thích cặn kẽ cơ chế kỹ thuật ngầm của từng tính năng. Mục tiêu là giúp bất kỳ thành viên nào trong nhóm hoặc người đọc cũng có thể nhanh chóng nắm bắt và tự tin trình bày.

---

## I. TỔNG QUAN KIẾN TRÚC HỆ THỐNG (ARCHITECTURE OVERVIEW)

Hãy hình dung hệ thống di trú giống như một **"Quy trình chuyển nhà tự động"**:
*   **On-Premise (Ngôi nhà cũ):** Là máy chủ nội bộ của doanh nghiệp, nơi ứng dụng **EduFlex** và cơ sở dữ liệu **SQL Server** đang chạy thực tế trên máy tính của bạn.
*   **AWS Cloud (Ngôi nhà mới):** Là nền tảng đám mây AWS (giả lập bằng **LocalStack** trên Docker) gồm 2 khu vực: khu vực chính ở **Singapore (ap-southeast-1)** và khu vực dự phòng thảm họa ở **Tokyo (ap-northeast-1)**.
*   **Migration Dashboard (Đơn vị vận chuyển):** Là ứng dụng web quản trị điều phối toàn bộ việc kiểm tra, đóng gói, vận chuyển dữ liệu từ nhà cũ sang nhà mới và giám sát an toàn.

```mermaid
graph TD
    subgraph On-Premise Environment (Local Host)
        App[EduFlex Application]
        DB[(Local SQL Server)]
    end

    subgraph AWS Cloud Environment (LocalStack)
        subgraph ap-southeast-1 (Singapore Region - Vùng chính)
            S3_SG[S3 Bucket: cloud-migration-backup-vinhuni]
            DB_SG[DynamoDB Table: cloud-migration-logs-vinhuni]
            L_SG[Lambda Function: cloud-migration-self-test-vinhuni]
        end
        subgraph ap-northeast-1 (Tokyo Region - Vùng dự phòng DR)
            S3_TY[S3 Bucket: cloud-migration-backup-vinhuni-tokyo]
            DB_TY[DynamoDB Table: cloud-migration-logs-vinhuni-tokyo]
            L_TY[Lambda Function: cloud-migration-self-test-vinhuni-tokyo]
        end
    end

    App -->|1. Test & Save Connection| DB
    App -->|2. Export DB json & zip| S3_SG
    App -->|3. Record Activity Logs| DB_SG
    L_SG -->|4. Validate Integrity| S3_SG
    
    %% Failover
    DB_SG -.->|5. Cross-Region Replication| DB_TY
    S3_SG -.->|6. Cross-Region Replication| S3_TY
```

### 1. Bản đồ các thư mục chứa mã nguồn thực tế (Files Mapping)
*   **Mã nguồn ứng dụng nguồn:** Nằm tại thư mục `D:\cloud-migration-platform\app\OnPremApp\EduFlex - ĐTĐM`. Mọi cấu hình kết nối CSDL thật sẽ được ghi vào file `appsettings.json` trong thư mục này.
*   **Mã nguồn Dashboard điều khiển:** Nằm tại thư mục `D:\cloud-migration-platform\app\MigrationDashboard\MigrationDashboard`.
*   **Mã nguồn hạ tầng Terraform:** Nằm tại thư mục `D:\cloud-migration-platform\terraform`. Terraform dùng để khai báo hạ tầng bằng code (IaC) giúp tự động khởi tạo S3, DynamoDB, Lambda trên LocalStack.

---

## II. HƯỚNG DẪN CẤU HÌNH & CHUẨN BỊ TRƯỚC DEMO

Để đảm bảo buổi demo chạy mượt mà 100%, hãy chuẩn bị môi trường theo các bước đơn giản sau:

1.  **Bước 1: Bật Docker Desktop:** Đảm bảo phần mềm Docker trên máy của bạn đã được khởi động.
2.  **Bước 2: Khởi động Cloud giả lập (LocalStack):**
    Mở Terminal tại thư mục `D:\cloud-migration-platform` và gõ lệnh:
    ```bash
    docker-compose up -d
    ```
    *Giải thích dễ hiểu:* Lệnh này giúp tạo ra một môi trường đám mây AWS ảo chạy ngay trên máy tính của bạn, không tốn tiền mạng và không lo phát sinh chi phí AWS thật.
3.  **Bước 3: Dựng hạ tầng tự động bằng Terraform:**
    Mở Terminal tại thư mục `D:\cloud-migration-platform\terraform` và gõ lệnh:
    ```bash
    terraform init
    terraform apply -auto-approve
    ```
    *Giải thích dễ hiểu:* Terraform sẽ tự động tạo ra các "hộp chứa dữ liệu" (S3 buc### TÍNH NĂNG 1: ĐÁNH GIÁ TRƯỚC DI TRÚ (PRE-MIGRATION ASSESSMENT)
*   **Ý nghĩa tính năng:** Tính năng này giống như **"Bác sĩ khám sức khỏe"** cho mã nguồn của ứng dụng. Trước khi chuyển ứng dụng lên đám mây, chúng ta phải quét xem code có tương thích với đám mây không, có lỗi thời không và có rủi ro bảo mật nào không.
*   **Cơ chế hoạt động kỹ thuật:**
    1.  Khi người dùng nhấn yêu cầu quét, Dashboard sẽ dùng dịch vụ `PreMigrationAssessmentService` đọc trực tiếp file project `EduFlex.csproj` và file cấu hình `appsettings.json` của ứng dụng nguồn.
    2.  Nó tiến hành phân tích cú pháp (Static Scan) bằng biểu thức chính quy (Regex) để tìm phiên bản .NET và các gói thư viện dữ liệu đang cài đặt.
*   **Giải thích các khái niệm cốt lõi cần hiểu:**
    *   **Readiness Score (Điểm sẵn sàng):** Thang điểm từ 0 đến 100 thể hiện mức độ tương thích đám mây:
        *   Nếu ứng dụng chạy .NET 8.0 trở lên: cộng 30 điểm (vì .NET Core mới hỗ trợ chạy đa nền tảng Linux/Docker rất tốt).
        *   Có cấu hình kết nối CSDL rõ ràng: cộng 20 điểm.
        *   Có đầy đủ các thư mục cấu trúc tiêu chuẩn MVC (`Controllers`, `Views`, `wwwroot`): cộng 50 điểm.
    *   **Rủi ro bảo mật (Architectural Risks):** Nếu phát hiện chuỗi kết nối chứa mật khẩu viết dưới dạng văn bản thuần (Plain-text) không mã hóa, hệ thống sẽ cảnh báo đỏ và khuyên nên dùng dịch vụ **AWS Secrets Manager** để lưu trữ an toàn.
    *   **Ước tính chi phí FinOps (FinOps Cost Analysis):** Tự động tính toán chi phí vận hành hàng tháng của hệ thống khi chạy trên đám mây. So sánh chi phí giữa mô hình di chuyển nguyên trạng (Rehost - EC2 & RDS SQL Server) và mô hình hiện đại hóa tối ưu (Serverless - AWS Lambda, DynamoDB & S3). Công thức tính toán dựa trên số lượng controllers (logic xử lý), dung lượng mã nguồn và số lượng tệp tin tĩnh (wwwroot) quét được từ mã nguồn EduFlex. Giúp doanh nghiệp tối ưu hóa chi phí lên đến 80% khi sử dụng mô hình Serverless.

---

### TÍNH NĂNG 2: CẤU HÌNH CSDL NGUỒN ĐỘNG & KIỂM TRA KẾT NỐI (TEST CONNECTION)
*   **Ý nghĩa tính năng:** Cho phép kết nối và kiểm tra xem Dashboard có giao tiếp được với cơ sở dữ liệu SQL Server thực tế (chứa dữ liệu thật của bạn) hay không.
*   **Cơ chế hoạt động kỹ thuật:**
    1.  Người dùng nhập các thông số: Địa chỉ Server (Host), Tên Database, phương thức đăng nhập (Windows Authentication hoặc SQL Authentication).
    2.  Hệ thống sử dụng thư viện kết nối **ADO.NET (`SqlConnection`)** thực hiện kết nối thử. 
    3.  **AJAX Live Validation:** Quá trình kết nối thử diễn ra hoàn toàn chạy ngầm dưới nền trang web (không cần tải lại trang). Kết quả thành công hoặc thất bại kèm mã lỗi chi tiết sẽ hiển thị ngay lập tức lên màn hình.
    4.  **Đồng bộ cấu hình tự động (Configuration Synchronization):** Khi người dùng nhấn nút *"Lưu cấu hình"*, Dashboard sẽ mở file cấu hình `appsettings.json` của ứng dụng nguồn `EduFlex` và tự động ghi đè chuỗi kết nối mới này vào đó.
*   **Giải thích các khái niệm cốt lõi cần hiểu:**
    *   **Dynamic Configuration Override:** Cơ chế này giúp toàn bộ hệ thống Dashboard và cả ứng dụng nguồn luôn luôn đồng bộ thông tin kết nối CSDL, giúp chuyển hướng sang các cơ sở dữ liệu thật khác nhau một cách dễ dàng chỉ bằng vài cú click chuột.

---

### TÍNH NĂNG 3: TRUNG TÂM CHUYỂN ĐỔI (MIGRATION CENTER) & HOÀN TÁC (ROLLBACK)
*   **Ý nghĩa tính năng:** Đây là **"Trái tim"** của hệ thống, thực hiện quy trình tự động hóa chuyển ứng dụng và dữ liệu lên AWS Cloud chỉ qua 1 nút bấm, và hỗ trợ nút hoàn tác dọn dẹp sạch tài nguyên nếu muốn làm lại.
*   **Cơ chế hoạt động kỹ thuật:**
    Quy trình di trú tự động chạy qua một đường ống dẫn (Pipeline) gồm 8 bước với thanh tiến trình hiển thị trực quan:
    1.  **Chuẩn bị (10%):** Tạo ra một mã định danh di trú duy nhất (`Migration ID` dạng UUID).
    2.  **Đánh giá (25%):** Xuất báo cáo kết quả quét mã nguồn sang tệp tin JSON.
    3.  **Đóng gói mã nguồn (40%):** Dùng thư viện nén dữ liệu đóng gói toàn bộ thư mục ứng dụng nguồn thành tệp tin `source-package.zip`.
    4.  **Trích xuất cơ sở dữ liệu (55%):** Hệ thống truy cập vào SQL Server của bạn, quét qua tất cả các bảng dữ liệu thực tế, đếm số dòng, trích xuất cấu trúc cột (metadata) và lấy ra tối đa 20 dòng dữ liệu mẫu của từng bảng để đóng gói thành tệp tin JSON `database-export.json`.
    5.  **Tải lên đám mây S3 (70%):** Tải toàn bộ tệp tin mã nguồn `.zip` và tệp dữ liệu CSDL `.json` lên dịch vụ lưu trữ AWS S3 Bucket.
    6.  **Kiểm định tự động AWS Lambda (85%):** Dashboard gọi lệnh kích hoạt một robot chạy ngầm không máy chủ (AWS Lambda Function). Đoạn mã Python của Lambda (`self_test.py`) sẽ kiểm tra xem các file backup tải lên S3 có đủ không và ghi log trạng thái thành công `SELF_TEST_PASSED` vào bảng DynamoDB.
    7.  **Khởi tạo báo cáo (95%):** Tạo tệp tổng kết quá trình.
    8.  **Hoàn thành (100%):** Ghi nhận trạng thái hoàn tất thành công.
*   **Hoàn tác di trú (Rollback):** Nếu bấm Rollback, hệ thống sẽ thực hiện dọn dẹp (Clean up): gọi AWS SDK để xóa các file backup trên S3 Bucket và xóa sạch các log hoạt động trong DynamoDB để đưa môi trường đám mây về trạng thái trống ban đầu.

---

### TÍNH NĂNG 4: LỊCH SỬ CHUYỂN ĐỔI (MIGRATION HISTORY)
*   **Ý nghĩa tính năng:** Giống như **"Cuốn sổ nhật ký hành trình"**, lưu lại tất cả thông tin của các lần chạy di trú trước đây để quản trị viên theo dõi.
*   **Cơ chế hoạt động kỹ thuật:**
    *   Hệ thống gọi API AWS SDK thực hiện quét toàn bộ (`ScanRequest`) bảng log trong DynamoDB.
    *   **Gom nhóm dữ liệu thông minh:** Vì DynamoDB lưu log dạng danh sách phẳng (flat list), hệ thống sẽ tự động gom nhóm (Group by) các bản ghi log có chung `MigrationId` lại với nhau. Sau đó sắp xếp các log theo mốc thời gian `Timestamp` để tính toán xem lần di trú đó mất tổng cộng bao nhiêu giây, có bao nhiêu cảnh báo/lỗi, và tiến độ phần trăm cao nhất đạt được là bao nhiêu.

---

### TÍNH NĂNG 5: PHÒNG ĐIỀU HÀNH KHẨN CẤP (DISASTER RECOVERY - DR ROOM)
*   **Ý nghĩa tính năng:** Giả lập tình huống thảm họa cực kỳ nguy hiểm trong thực tế (ví dụ: đứt cáp quang biển, cháy trung tâm dữ liệu ở Singapore) và kiểm tra khả năng tự động ứng cứu, chuyển vùng hoạt động sang trung tâm dữ liệu dự phòng ở Tokyo.
*   **Cơ chế hoạt động kỹ thuật:**
    1.  **Singapore Outage (Sập vùng):** Khi bấm nút giả lập sập, Dashboard sẽ cố tình kích hoạt trạng thái ngắt kết nối đến khu vực chính. Khi đó, toàn bộ giao diện chính của Dashboard sẽ bị khóa, chuyển sang màu đỏ cảnh báo mất kết nối dịch vụ.
    2.  **Tokyo Failover (Chuyển vùng khẩn cấp):** Khi quản trị viên bấm nút bắt đầu phục hồi thảm họa, Dashboard sẽ thực thi kịch bản chuyển đổi vùng hoạt động mặc định sang Tokyo (`ap-northeast-1`).
    3.  **Tự động dựng hạ tầng bằng Terraform:** Dashboard kích hoạt Terraform để tự động khởi tạo hạ tầng dự phòng (S3 Bucket và bảng DynamoDB mới) tại vùng Tokyo.
    4.  **Cross-Region Replication (CRR - Sao chép xuyên vùng):** Dữ liệu sao lưu và logs hoạt động từ vùng Singapore trước khi sập đã được sao chép tự động sang Tokyo, hệ thống tại Tokyo sẽ đọc các tệp này để phục hồi lại trạng thái ứng dụng.
*   **Giải thích các khái niệm cốt lõi cần hiểu:**
    *   **RTO (Recovery Time Objective - Thời gian phục hồi thực tế):** Khoảng thời gian từ lúc Singapore sập cho đến khi Tokyo hoạt động bình thường trở lại (trong demo chỉ mất khoảng 8 giây).
    *   **RPO (Recovery Point Objective - Mức độ mất dữ liệu):** Nhờ cơ chế sao chép dữ liệu xuyên vùng tự động (CRR), dữ liệu tại Singapore luôn có bản sao tức thời tại Tokyo, giúp **RPO đạt 0.00 giây** (không mất mát bất kỳ byte dữ liệu nào của doanh nghiệp).

---

### TÍNH NĂNG 6: GIÁM SÁT VẬN HÀNH (OPERATIONS MONITORING)
*   **Ý nghĩa tính năng:** Giống như **"Bảng đồng hồ công-tơ-mét"** của xe ô tô, hiển thị sức khỏe và hiệu năng hoạt động của hệ thống máy chủ đám mây sau khi di trú thành công dưới dạng biểu đồ động thời gian thực.
*   **Cơ chế hoạt động kỹ thuật:**
    *   Hệ thống tích hợp thư viện **Chart.js** để vẽ biểu đồ đường (Line Chart) động cập nhật liên tục mỗi 2 giây, đo lường các thông số **CPU Utilization (%)** và **Memory Usage (%)** của hạ tầng đám mây.
    *   Hiển thị thông số dạng số của **CPU Utilization**, **Memory Usage (MB)**, và **Network Traffic (KB/s)** ở bảng điều khiển bên cạnh.
    *   Mô phỏng chân thực: Khi có các thao tác di trú hoặc failover, biểu đồ hiệu năng sẽ hiển thị các đỉnh nhọn (spikes) tải tăng vọt tạm thời, phản ánh đúng thực tế tài nguyên đám mây hoạt động lúc bận rộn.
    *   Vẽ biểu đồ hình tròn (Doughnut Chart) thống kê phân phối trạng thái log thu được từ DynamoDB.�ng hoạt động sang trung tâm dữ liệu dự phòng ở Tokyo.
*   **Cơ chế hoạt động kỹ thuật:**
    1.  **Singapore Outage (Sập vùng):** Khi bấm nút giả lập sập, Dashboard sẽ cố tình kích hoạt trạng thái ngắt kết nối đến khu vực chính. Khi đó, toàn bộ giao diện chính của Dashboard sẽ bị khóa, chuyển sang màu đỏ cảnh báo mất kết nối dịch vụ.
    2.  **Tokyo Failover (Chuyển vùng khẩn cấp):** Khi quản trị viên bấm nút bắt đầu phục hồi thảm họa, Dashboard sẽ thực thi kịch bản chuyển đổi vùng hoạt động mặc định sang Tokyo (`ap-northeast-1`).
    3.  **Tự động dựng hạ tầng bằng Terraform:** Dashboard kích hoạt Terraform để tự động khởi tạo hạ tầng dự phòng (S3 Bucket và bảng DynamoDB mới) tại vùng Tokyo.
    4.  **Cross-Region Replication (CRR - Sao chép xuyên vùng):** Dữ liệu sao lưu và logs hoạt động từ vùng Singapore trước khi sập đã được sao chép tự động sang Tokyo, hệ thống tại Tokyo sẽ đọc các tệp này để phục hồi lại trạng thái ứng dụng.
*   **Giải thích các khái niệm cốt lõi cần hiểu:**
    *   **RTO (Recovery Time Objective - Thời gian phục hồi thực tế):** Khoảng thời gian từ lúc Singapore sập cho đến khi Tokyo hoạt động bình thường trở lại (trong demo chỉ mất khoảng 8 giây).
    *   **RPO (Recovery Point Objective - Mức độ mất dữ liệu):** Nhờ cơ chế sao chép dữ liệu xuyên vùng tự động (CRR), dữ liệu tại Singapore luôn có bản sao tức thời tại Tokyo, giúp **RPO đạt 0.00 giây** (không mất mát bất kỳ byte dữ liệu nào của doanh nghiệp).

---

### TÍNH NĂNG 6: GIÁM SÁT VẬN HÀNH (OPERATIONS MONITORING)
*   **Ý nghĩa tính năng:** Giống như **"Bảng đồng hồ công-tơ-mét"** của xe ô tô, hiển thị sức khỏe và hiệu năng hoạt động của hệ thống máy chủ đám mây sau khi di trú thành công.
*   **Cơ chế hoạt động kỹ thuật:**
    *   Hệ thống vẽ biểu đồ thời gian thực (Real-time chart) mô phỏng dịch vụ **AWS CloudWatch** đo lường các thông số: **CPU Utilization (Hiệu suất CPU), Memory Usage (Bộ nhớ RAM), Storage (Ổ đĩa), Network I/O (Băng thông mạng)**.
    *   Dữ liệu hiệu năng được cập nhật tự động liên tục qua các truy vấn AJAX chạy ngầm.

---

### TÍNH NĂNG 7: BẢO MẬT & TUÂN THỦ (SECURITY & COMPLIANCE)
*   **Ý nghĩa tính năng:** Giống như **"Thanh tra an ninh mạng"**, kiểm tra xem hạ tầng đám mây khai báo bằng code Terraform của chúng ta có được bảo mật tốt và tuân thủ các quy tắc an toàn quốc tế không.
*   **Cơ chế hoạt động kỹ thuật:**
    *   Hệ thống phân tích cú pháp tĩnh của các file cấu hình Terraform (`*.tf`) trong thư mục `terraform`.
    *   Nó đối chiếu cấu hình hạ tầng với tiêu chuẩn **AWS Well-Architected Framework** ở 4 khía cạnh:
        1.  *Mã hóa dữ liệu tại chỗ (SSE-KMS Encryption):* Các file lưu trên S3 có được khóa bằng mật mã không?
        2.  *Khóa truy cập công cộng (Public Access Block):* S3 bucket có bị lộ ra ngoài Internet cho người lạ xem trộm không?
        3.  *Phân quyền tối thiểu (Least Privilege IAM):* Các tài nguyên có được cấp quyền vừa đủ để chạy không, hay cấp quyền quá rộng dễ bị hack?
        4.  *Mạng nội bộ an toàn (VPC Private Subnet):* Cơ sở dữ liệu và mã nguồn có được giấu sau tường lửa trong mạng nội bộ không?
    *   Từ đó tính ra điểm phần trăm tuân thủ bảo mật và đưa ra hướng dẫn khắc phục cụ thể.

---

### TÍNH NĂNG 8: QUẢN LÝ TÀI NGUYÊN CLOUD (CLOUD RESOURCES)
*   **Ý nghĩa tính năng:** Giao diện quản lý file và nhật ký trực quan, cho phép người dùng **"Nhìn tận mắt, sờ tận tay"** các tài nguyên đám mây thực tế đang nằm trên LocalStack.
*   **Cơ chế hoạt động kỹ thuật:**
    *   Dashboard sử dụng thư viện **AWS SDK (`AmazonS3Client` và `AmazonDynamoDBClient`)** truy vấn trực tiếp vào LocalStack.
    *   Hiển thị danh sách và kích thước của các file backup (`source-package.zip`, `database-export.json`) trong S3 Bucket.
    *   Hiển thị chi tiết từng dòng nhật ký hoạt động đang được lưu trữ thực tế bên trong bảng logs DynamoDB.
