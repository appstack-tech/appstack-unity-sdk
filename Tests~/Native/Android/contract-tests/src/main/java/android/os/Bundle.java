package android.os;

import java.util.HashMap;
import java.util.Map;

public class Bundle {
    private final Map<String, String> strings = new HashMap<>();

    public void putString(String key, String value) {
        strings.put(key, value);
    }

    public String getString(String key) {
        return strings.get(key);
    }
}
