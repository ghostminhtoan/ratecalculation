# Quy trình hoạt động (Workflow)

## 1. Khởi tạo số dư
- Người dùng nhập số tiền ban đầu tại **Panel 1** và bấm **Khởi tạo**.
- Số dư ban đầu và số dư hiện tại được thiết lập.
- Hệ thống tự động lưu trữ thông tin xuống file `session_data.txt` ở thư mục chạy ứng dụng để duy trì phiên làm việc khi tắt app.

## 2. Thao tác đặt cược nhanh trên Panel 2
- **Click chuột trái**: Cộng thêm giá trị chip được chọn vào ô cược.
- **Click chuột phải**: Hoàn tác (Undo) 1 bước đặt cược gần nhất trên ô đó.
- **Click chuột giữa**: Xóa sạch toàn bộ số tiền cược đang có trên ô đó.
- **Ctrl + Z**: Hoàn tác từng bước đặt cược trên toàn bộ bàn chơi theo trình tự thời gian.

## 3. Thiết lập kết quả & Áp dụng
- Bấm vào các nút **W (Thắng)** / **D (Hòa)** / **L (Thua)** nhỏ bên phải của mỗi ô cược để chọn kết quả mong muốn cho ô đó (nút sẽ chuyển trạng thái giữ và sáng màu).
- Bấm nút **⚡ APPLY** lớn ở góc dưới Panel 2 để áp dụng kết quả của toàn bộ các ô đã chọn và tính toán kết quả chung cho 1 ván:
  - Cập nhật số dư hiện tại dựa trên tỷ lệ thanh toán của từng ô.
  - Reset trạng thái các nút cược và nút kết quả về ban đầu.
  - Ghi nhận thông tin chi tiết của ván đấu vào **Panel 3** (Lịch sử kết quả) và lưu lại trạng thái số dư.

## 4. Nguyên tắc biên dịch và Kiểm tra lỗi (Build & Test Rules)
- Khi build phải check xem có error hoặc warning hay không. Nếu có thì phải fix bug đến khi không còn error hoặc warning nữa thì mới thôi.
- Sau khi xong thì phải mở app lên test xem có chạy được không hay bị crash. Nếu bị crash thì phải tiếp tục fix bug đến khi mở app lên chạy được bình thường mới xem như là hoàn thành.
