import socket
import json
import threading
import time
import os


class AUVTerminal:
    def __init__(self, ip="127.0.0.1", port=8080):
        self.target_addr = (ip, port)
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.telemetry = {1: None, 2: None, 3: None}
        self.is_running = True

        # Фоновый поток для получения данных
        self.listener = threading.Thread(target=self._receive_loop, daemon=True)
        self.listener.start()

    def _receive_loop(self):
        while self.is_running:
            try:
                self.sock.settimeout(0.5)
                data, _ = self.sock.recvfrom(4096)
                msg = json.loads(data.decode('utf-8'))
                auv_id = msg.get("auv_id")
                if auv_id in self.telemetry:
                    self.telemetry[auv_id] = msg
            except:
                continue

    def send(self, auv_id, cmd, val):
        payload = {"auv_id": auv_id, "command": cmd, "value": float(val)}
        self.sock.sendto(json.dumps(payload).encode('utf-8'), self.target_addr)

    def print_help(self):
        help_text = """
        ╔════════════════════════════════════════════════════════════════╗
        ║          ДОКУМЕНТАЦИЯ СИСТЕМЫ УПРАВЛЕНИЯ АНПА v2.0             ║
        ╚════════════════════════════════════════════════════════════════╝

        ОСНОВНОЙ СИНТАКСИС: [ID] [КОМАНДА] [ЗНАЧЕНИЕ]
        Пример: 1 move_forward 10.5

        --- КОМАНДЫ НАВИГАЦИИ ---
        • move_forward  - Движение вперед (сила/скорость).
        • move_backward - Движение назад.
        • rotate        - Поворот по курсу (Yaw). >0 вправо, <0 влево.
        • set_depth     - Погружение на целевую глубину (в метрах).
        • stop          - Полная остановка двигателей.

        --- СИСТЕМНЫЕ КОМАНДЫ ТЕРМИНАЛА ---
        • /tele [ID]    - Вывести подробную телеметрию аппарата.
        • /status       - Проверить связь со всеми 3-мя аппаратами.
        • /clear        - Очистить экран консоли.
        • /help         - Показать эту справку.
        • /q            - Завершить сеанс связи.

        --- ПАРАМЕТРЫ ТЕЛЕМЕТРИИ (ОТВЕТ ОТ UNITY) ---
        • Lat/Lon  - Географические координаты.
        • Alt      - Высота над дном (эхолот).
        • Depth    - Текущая глубина.
        • Pitch    - Дифферент (наклон носа вверх/вниз).
        • Yaw      - Курс (0-360 градусов).
        • Roll     - Крен аппарата.
        ╚════════════════════════════════════════════════════════════════╝
        """
        print(help_text)

    def show_status(self):
        print("\n СТАТУС ПОДКЛЮЧЕНИЯ ОБЪЕКТОВ:")
        for i in range(1, 4):
            status = "ОНЛАЙН" if self.telemetry[i] else "НЕТ ДАННЫХ"
            print(f"  [АНПА #{i}]: {status}")
        print("")

    def show_telemetry(self, auv_id):
        data = self.telemetry.get(auv_id)
        if not data:
            print(f"[-] Данные от АНПА #{auv_id} еще не поступали.")
            return

        print(f"\n--- ТЕЛЕМЕТРИЯ АНПА #{auv_id} ---")
        print(f" Позиция: Lat: {data['lat']:.5f}, Lon: {data['lon']:.5f}")
        print(f" Глубина: {data['depth']:.2f} м | От дна: {data['alt']:.2f} м")
        print(f" Углы:    Курс(Yaw): {data['yaw']:.1f}°, Диф(Pitch): {data['pitch']:.1f}°, Крен: {data['roll']:.1f}°")
        print(f" Скорость: {data['velocity']:.2f} м/с")
        print("-" * 30)


def main():
    terminal = AUVTerminal()
    # При старте сразу выводим документацию
    terminal.print_help()

    while True:
        try:
            cmd_input = input("PRO_TERMINAL >> ").strip().lower()
            if not cmd_input: continue

            if cmd_input == "/q":
                break
            elif cmd_input == "/help":
                terminal.print_help()
            elif cmd_input == "/status":
                terminal.show_status()
            elif cmd_input == "/clear":
                os.system('cls' if os.name == 'nt' else 'clear')
            elif cmd_input.startswith("/tele"):
                parts = cmd_input.split()
                if len(parts) == 2:
                    terminal.show_telemetry(int(parts[1]))
                else:
                    print("Укажите ID: /tele 1")

            # Обработка команд управления: ID CMD VAL
            else:
                parts = cmd_input.split()
                if len(parts) == 3:
                    auv_id, cmd, val = int(parts[0]), parts[1], float(parts[2])
                    if 1 <= auv_id <= 3:
                        terminal.send(auv_id, cmd, val)
                        print(f"[SENT] Команда {cmd}({val}) отправлена на АНПА #{auv_id}")
                    else:
                        print("[!] Ошибка: ID должен быть 1, 2 или 3.")
                else:
                    print("[!] Неверный формат. Используйте: ID COMMAND VALUE или /help")

        except Exception as e:
            print(f"[ОШИБКА]: {e}")

    terminal.is_running = False
    print("Терминал закрыт.")


if __name__ == "__main__":
    main()