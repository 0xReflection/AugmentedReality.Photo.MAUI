package com.example.ar;

import android.net.Uri;
import android.os.Bundle;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import com.google.ar.core.Anchor;
import com.google.ar.sceneform.AnchorNode;
import com.google.ar.sceneform.rendering.ModelRenderable;
import com.google.ar.sceneform.ux.ArFragment;
import com.google.ar.sceneform.ux.TransformableNode;

public class ArActivity extends AppCompatActivity {

    private ArFragment arFragment;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_ar);

        // Находим ARFragment в layout
        arFragment = (ArFragment) getSupportFragmentManager()
                .findFragmentById(R.id.arFragment);

        // Слушатель для тапов по плоскости
        if (arFragment != null) {
            arFragment.setOnTapArPlaneListener((hitResult, plane, motionEvent) -> {
                if (hitResult == null) return;

                // Создаём anchor (точку в пространстве AR)
                Anchor anchor = hitResult.createAnchor();

                // Загружаем 3D модель
                ModelRenderable.builder()
                        // модель лежит в /Assets/Models/hero1.glb
                        .setSource(this, Uri.parse("hero1.glb"))
                        .setIsFilamentGltf(true) // для GLB/GLTF
                        .build()
                        .thenAccept(renderable -> placeModel(anchor, renderable))
                        .exceptionally(throwable -> {
                            Toast.makeText(this,
                                    "Ошибка загрузки модели: " + throwable.getMessage(),
                                    Toast.LENGTH_LONG).show();
                            return null;
                        });
            });
        }
    }

    // Метод для размещения модели
    private void placeModel(Anchor anchor, ModelRenderable renderable) {
        AnchorNode anchorNode = new AnchorNode(anchor);
        anchorNode.setParent(arFragment.getArSceneView().getScene());

        TransformableNode modelNode = new TransformableNode(arFragment.getTransformationSystem());
        modelNode.setParent(anchorNode);
        modelNode.setRenderable(renderable);
        modelNode.select();
    }
}
