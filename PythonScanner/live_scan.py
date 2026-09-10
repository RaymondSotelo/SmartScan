import base64
import cv2
import os
from flask import Flask, jsonify, request
from flask_cors import CORS
import numpy as np
from ultralytics import YOLO

app = Flask(__name__)
CORS(app)

# Dynamically get the exact folder where live_scan.py is located
current_dir = os.path.dirname(os.path.abspath(__file__))

# Build the path to the model inside the project folder
# NOTE: If your actual model file is named 'yolo11s.pt', change 'my_model.pt' below to 'yolo11s.pt'
model_path = os.path.join(current_dir, "my_model", "my_model.pt")

model = YOLO(model_path)

@app.route("/detect_frame", methods=["POST"])
def detect_frame():
    data = request.get_json()
    if not data or "image" not in data:
        return jsonify({"error": "No image data provided"}), 400

    try:
        encoded_data = data["image"].split(",")[1]
        image_bytes = base64.b64decode(encoded_data)
        np_arr = np.frombuffer(image_bytes, np.uint8)
        img = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
    except Exception as e:
        return jsonify({"error": f"Failed to process image: {str(e)}"}), 400

    results = model(img)
    detections = []

    for r in results:
        for box in r.boxes:
            conf = float(box.conf[0])
            cls_id = int(box.cls[0])
            name = model.names[cls_id]
            coords = list(map(int, box.xyxy[0]))  # [x1, y1, x2, y2]

            # Always include the box coordinates and confidence flag
            detections.append({
                "name": name,
                "confidence": round(conf * 100, 1),
                "is_confident": conf >= 0.80,  # True if 80% or above
                "box": coords,
            })

    return jsonify({"detections": detections})


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=False)