import cv2
import mediapipe as mp
import json
import socket  # ★UDP通信用のライブラリを追加

# --- UDP通信の設定 ---
UDP_IP = "127.0.0.1"  # 自分自身のPC（ローカルホスト）を指定
UDP_PORT = 5005       # Unity側と一致させるポート番号
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) # UDPソケットの作成

# MediaPipeの準備
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles
mp_face_mesh = mp.solutions.face_mesh
mp_hands = mp.solutions.hands
mp_pose = mp.solutions.pose

cap = cv2.VideoCapture(0)
window_name = 'VTuber Tracker'
cv2.namedWindow(window_name, cv2.WINDOW_NORMAL)
cv2.setWindowProperty(window_name, cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_FULLSCREEN)

with mp_face_mesh.FaceMesh(max_num_faces=1, refine_landmarks=True, min_detection_confidence=0.5, min_tracking_confidence=0.5) as face_mesh, \
     mp_hands.Hands(max_num_hands=2, min_detection_confidence=0.5, min_tracking_confidence=0.5) as hands, \
     mp_pose.Pose(min_detection_confidence=0.5, min_tracking_confidence=0.5) as pose:

    while cap.isOpened():
        ret, frame = cap.read()
        if not ret:
            break

        image = cv2.cvtColor(cv2.flip(frame, 1), cv2.COLOR_BGR2RGB)
        image.flags.writeable = False

        face_results = face_mesh.process(image)
        hand_results = hands.process(image)
        pose_results = pose.process(image)

        image.flags.writeable = True
        image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)

        # データ収集用辞書
        vtuber_data = {"pose": {}, "face": {}}

        # 1. 体の骨格（Pose）データの格納
        if pose_results.pose_landmarks:
            for landmark_id, landmark in enumerate(pose_results.pose_landmarks.landmark):
                landmark_name = mp_pose.PoseLandmark(landmark_id).name
                # Unityと合わせやすいよう、Y座標を反転（MediaPipeは下方向がプラス、Unityは上方向がプラスのため）
                vtuber_data["pose"][landmark_name] = {
                    "x": landmark.x,
                    "y": 1.0 - landmark.y, # 上下反転
                    "z": landmark.z
                }
            mp_drawing.draw_landmarks(image, pose_results.pose_landmarks, mp_pose.POSE_CONNECTIONS, landmark_drawing_spec=mp_drawing_styles.get_default_pose_landmarks_style())

        # 2. 顔（Face）データの格納（代表地点のみ）
        if face_results.multi_face_landmarks:
            face_landmarks = face_results.multi_face_landmarks[0]
            target_indices = {"right_eye": 468, "left_eye": 473, "upper_lip": 13, "lower_lip": 14}
            for part_name, index in target_indices.items():
                lm = face_landmarks.landmark[index]
                vtuber_data["face"][part_name] = {
                    "x": lm.x,
                    "y": 1.0 - lm.y, # 上下反転
                    "z": lm.z
                }
            mp_drawing.draw_landmarks(image, face_landmarks, mp_face_mesh.FACEMESH_CONTOURS, None, mp_drawing_styles.get_default_face_mesh_contours_style())

        # --- ★ JSONに変換してUnityへUDP送信 ---
        if vtuber_data["pose"] or vtuber_data["face"]:
            json_data = json.dumps(vtuber_data)
            
            # 文字列をバイナリ（bytes）に変換して送信
            sock.sendto(json_data.encode('utf-8'), (UDP_IP, UDP_PORT))

        cv2.imshow(window_name, image)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cap.release()
cv2.destroyAllWindows()