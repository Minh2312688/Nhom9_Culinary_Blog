# Thiết kế đồng bộ tài liệu theo trạng thái dự án

## Mục tiêu

Rà soát tài liệu dự án và chỉnh các mô tả trạng thái/hướng dẫn để chúng phản
ánh mã nguồn, kiểm thử và cấu hình hiện có, đồng thời giữ được lịch sử quyết
định của nhóm.

## Phạm vi

- Rà soát tài liệu Markdown đã theo dõi trong thư mục gốc và `docs/`, gồm README,
  hướng dẫn vận hành, đặc tả/checklist tính năng, SRS/audit, kế hoạch và thiết kế.
- Sửa các khẳng định hiện trạng, checklist, hướng dẫn chạy, kiểm thử và liên kết
  không còn đúng; thống nhất thuật ngữ và trạng thái giữa các tài liệu có quan hệ.
- Không sửa mã nguồn, test, cấu hình triển khai hay dependency manifest.
- Giữ nguyên `Bao_cao_Culinary_Blog.docx` vì đây là tệp chưa được Git theo dõi và
  nằm ngoài kho tài liệu Markdown được duyệt.

## Nguyên tắc nguồn thông tin

1. Hành vi trong mã nguồn và kiểm thử hiện tại là căn cứ cho trạng thái tính
   năng; cấu hình Compose/app là căn cứ cho cách chạy và seed.
2. Tài liệu yêu cầu/SRS vẫn là lịch sử yêu cầu. Không đổi nội dung yêu cầu ban
   đầu để khiến nó giống như yêu cầu được viết sau khi triển khai.
3. Với kế hoạch/đặc tả có ngày tháng, giữ lại mục tiêu và quyết định tại thời
   điểm viết; cập nhật checklist hoặc thêm ghi chú trạng thái có ngày, đồng thời
   nêu rõ nội dung đã thay thế nếu trạng thái mới khác kế hoạch cũ.
4. Chỉ khẳng định “đã xác minh” khi có bằng chứng test/command tương ứng; phân
   biệt chức năng đã có code với chức năng đã được kiểm thử trong môi trường
   cần thiết.
5. Không suy diễn tác giả từ lịch sử Git hoặc hiện diện của mã. Thông tin thành
   viên chỉ cập nhật khi tài liệu hiện hành đã có xác nhận.

## Phân nhóm tài liệu và xử lý

- **Tổng quan và vận hành:** cập nhật mô tả tính năng, yêu cầu môi trường, cấu
  hình, ports, lệnh chạy/test và các liên kết đến nguồn hướng dẫn chi tiết.
- **Tài liệu trạng thái/triển khai:** đối chiếu từng mục đã làm, một phần, còn
  mở hoặc đã thay thế với repository; gỡ hướng dẫn dựa trên thành phần không còn
  được dùng.
- **Đặc tả và checklist tính năng:** giữ các yêu cầu gốc, cập nhật kết quả thực
  hiện và ghi chú những giới hạn/xác minh còn thiếu.
- **SRS, audit, kế hoạch và thiết kế lịch sử:** không viết lại lịch sử; ghi nhận
  trạng thái hiện tại ở phần phù hợp và trỏ tới hướng dẫn/đặc tả hiện hành khi
  tài liệu cũ không còn là nguồn vận hành.

## Kiểm chứng

- Đối chiếu mô tả recipe, authorization, seed, dashboard, ảnh/bước/dinh dưỡng,
  caching, migration và test với code/config/test liên quan.
- Kiểm tra đường dẫn và liên kết nội bộ còn trỏ tới tài liệu tồn tại.
- Đọc diff để bảo đảm thay đổi chỉ thuộc tài liệu đã duyệt, không đưa secret vào
  tài liệu và không thay đổi nội dung người dùng chưa theo dõi.
- Không chạy build/test ứng dụng cho thay đổi tài liệu thuần túy.
